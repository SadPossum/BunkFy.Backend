namespace BunkFy.Modules.DataRights.Application.Handlers;

using BunkFy.Modules.DataRights.Application.Commands;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Models;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;

internal sealed class CompleteTenantTerminationVerificationCommandHandler(
    ITenantTerminationRepository repository,
    TenantTerminationMutationCoordinator mutations,
    ITenantTerminationTerminalReceiptRepository receipts,
    TenantTerminationVerificationPlanner planner,
    ISystemClock clock)
    : ICommandHandler<
        CompleteTenantTerminationVerificationCommand,
        TenantTerminationVerificationCompleted>
{
    public async Task<Result<TenantTerminationVerificationCompleted>>
        HandleAsync(
            CompleteTenantTerminationVerificationCommand command,
            CancellationToken cancellationToken)
    {
        if (!HasValidCommandShape(command))
        {
            return Invalid();
        }

        TenantTerminationMutationState? state =
            await mutations.AcquireProcessAndCaseAsync(
            command.ProcessId,
            cancellationToken).ConfigureAwait(false);
        TenantTerminationProcess? process = state?.Process;
        if (process is null)
        {
            return Result.Failure<TenantTerminationVerificationCompleted>(
                DataRightsApplicationErrors.TenantTerminationProcessNotFound);
        }

        if (process.Phase != TenantTerminationProcessPhase.Verify ||
            process.Status != TenantTerminationProcessStatus.Running ||
            process.Version != command.ExpectedProcessVersion ||
            process.OperationRevision !=
                command.VerificationOperationRevision ||
            process.DestroyCompletedOperationRevision !=
                command.DestroyOperationRevision)
        {
            return Invalid();
        }

        DataRightsCase? dataRightsCase = state!.Case;
        if (dataRightsCase is null ||
            dataRightsCase.Status != DataRightsCaseState.Executing ||
            dataRightsCase.DecisionRevision != process.ApprovalRevision ||
            !TenantTerminationApprovalEvidenceContract.FixedTimeSha256Equals(
                dataRightsCase.TenantTerminationPolicyEvidenceSha256,
                process.PolicyEvidenceSha256))
        {
            return Invalid();
        }

        IReadOnlyList<TenantTerminationOwnerWorkItem> workItems =
            await repository.ListOwnerWorkItemsAsync(
                process.Id,
                TenantTerminationOwnerPhase.Destroy,
                command.DestroyOperationRevision,
                cancellationToken).ConfigureAwait(false);
        Result<TenantTerminationVerificationPlan> prepared = planner.Prepare(
            process,
            workItems);
        if (prepared.IsFailure ||
            !string.Equals(
                prepared.Value.OwnerProofSetSha256,
                command.ExpectedOwnerProofSetSha256,
                StringComparison.Ordinal) ||
            !string.Equals(
                prepared.Value.TerminalOwnerKey,
                command.ExpectedTerminalOwnerKey,
                StringComparison.Ordinal))
        {
            return Invalid();
        }

        TenantTerminationOwnerWorkItem terminalOwner = prepared.Value.WorkItems
            .Single(item => string.Equals(
                item.OwnerKey,
                prepared.Value.TerminalOwnerKey,
                StringComparison.Ordinal));
        DateTimeOffset latestOwnerProofAtUtc = prepared.Value.WorkItems.Max(
            item => item.ResultRecordedAtUtc.GetValueOrDefault());
        if (!HasValidCheckpoint(
                command.ReplayCheckpoint,
                process.Id,
                prepared.Value.WorkItems.Count,
                latestOwnerProofAtUtc) ||
            terminalOwner.SelectedProofRevision is not long selectedRevision ||
            terminalOwner.ResultingProofRevision is not long resultingRevision)
        {
            return Invalid();
        }

        Guid receiptId = TenantTerminationExecutionIdentity
            .CreateTerminalReceiptId(
                process.Id,
                process.OperationRevision);
        Guid idempotencyKey = TenantTerminationExecutionIdentity
            .CreateTerminalReceiptIdempotencyKey(
                process.Id,
                process.OperationRevision);
        DateTimeOffset nowUtc = clock.UtcNow;
        if (nowUtc < command.ReplayCheckpoint.FlushedAtUtc)
        {
            return Invalid();
        }

        TenantTerminationTerminalReceipt? receipt = await receipts.GetAsync(
            receiptId,
            cancellationToken).ConfigureAwait(false);
        receipt ??= await receipts.GetByIdempotencyKeyAsync(
            idempotencyKey,
            cancellationToken).ConfigureAwait(false);
        receipt ??= await receipts.GetByProcessAsync(
            process.Id,
            process.OperationRevision,
            cancellationToken).ConfigureAwait(false);
        if (receipt is null)
        {
            Result<TenantTerminationTerminalReceipt> sealedReceipt =
                TenantTerminationTerminalReceipt.Seal(
                    receiptId,
                    process.ScopeId,
                    idempotencyKey,
                    process.Id,
                    process.CaseId,
                    process.ApprovalRevision,
                    process.TerminationEpoch,
                    command.DestroyOperationRevision,
                    command.VerificationOperationRevision,
                    command.TaskRunId,
                    command.TaskAttempt,
                    process.PolicyEvidenceSha256,
                    process.FrozenRevisionSha256!,
                    process.ExportRequested,
                    process.ExportArtifactId,
                    process.ExportArtifactVersion,
                    process.ExportFragmentSetSha256,
                    prepared.Value.WorkItems.Count,
                    prepared.Value.OwnerProofSetSha256,
                    prepared.Value.TerminalOwnerKey,
                    selectedRevision,
                    resultingRevision,
                    command.ReplayCheckpoint.Cursor.Sequence,
                    command.ReplayCheckpoint.Cursor.RecordSha256,
                    command.ReplayCheckpoint.IntegrityKeyVersion,
                    command.ReplayCheckpoint.CheckpointProofSha256,
                    command.ReplayCheckpoint.FlushedAtUtc,
                    TenantTerminationCoordination.ExecutorActorId,
                    nowUtc);
            if (sealedReceipt.IsFailure)
            {
                return Result.Failure<
                    TenantTerminationVerificationCompleted>(
                        sealedReceipt.Error);
            }

            receipt = sealedReceipt.Value;
            await receipts.AddAsync(
                receipt,
                cancellationToken).ConfigureAwait(false);
        }
        else if (!MatchesReceipt(
            receipt,
            process,
            command,
            prepared.Value,
            terminalOwner))
        {
            return Invalid();
        }

        Result confirmed = process.ConfirmVerification(
            command.VerificationOperationRevision,
            receipt.Id,
            receipt.Version,
            prepared.Value.OwnerProofSetSha256,
            process.Version,
            TenantTerminationCoordination.ExecutorActorId,
            nowUtc);
        if (confirmed.IsFailure)
        {
            return Result.Failure<TenantTerminationVerificationCompleted>(
                confirmed.Error);
        }

        Result completed = process.CompletePhase(
            TenantTerminationProcessPhase.Verify,
            command.VerificationOperationRevision,
            process.Version,
            TenantTerminationCoordination.ExecutorActorId,
            nowUtc);
        if (completed.IsFailure)
        {
            return Result.Failure<TenantTerminationVerificationCompleted>(
                completed.Error);
        }

        Result caseCompleted = dataRightsCase
            .CompleteTenantTerminationExecution(
                process.ApprovalRevision,
                process.PolicyEvidenceSha256,
                dataRightsCase.Version,
                TenantTerminationCoordination.ExecutorActorId,
                nowUtc);
        if (caseCompleted.IsFailure)
        {
            return Result.Failure<TenantTerminationVerificationCompleted>(
                caseCompleted.Error);
        }

        return Result.Success(new TenantTerminationVerificationCompleted(
            process.Id,
            process.Version,
            receipt.Id,
            receipt.Version));
    }

    private static bool HasValidCommandShape(
        CompleteTenantTerminationVerificationCommand command) =>
        command.ProcessId != Guid.Empty &&
        command.DestroyOperationRevision > 0 &&
        command.VerificationOperationRevision >
            command.DestroyOperationRevision &&
        command.TaskRunId == TenantTerminationExecutionIdentity
            .CreateVerificationTaskRunId(
                command.ProcessId,
                command.VerificationOperationRevision) &&
        command.TaskAttempt > 0 &&
        command.ExpectedProcessVersion > 0 &&
        TenantTerminationReplayProof.IsSha256(
            command.ExpectedOwnerProofSetSha256) &&
        TenantTerminationReplayProof.IsStableCode(
            command.ExpectedTerminalOwnerKey,
            TenantTerminationContract.OwnerKeyMaxLength) &&
        command.ReplayCheckpoint is not null;

    private static bool HasValidCheckpoint(
        TenantTerminationReplayCheckpoint checkpoint,
        Guid processId,
        int ownerCount,
        DateTimeOffset latestOwnerProofAtUtc) =>
        checkpoint.ContractVersion ==
            TenantTerminationReplayCheckpoint.CurrentContractVersion &&
        checkpoint.ProcessId == processId &&
        checkpoint.Cursor is not null &&
        checkpoint.Cursor.Sequence >= ownerCount * 2L &&
        TenantTerminationReplayProof.IsSha256(
            checkpoint.Cursor.RecordSha256) &&
        checkpoint.IntegrityKeyVersion > 0 &&
        TenantTerminationReplayProof.IsSha256(
            checkpoint.CheckpointProofSha256) &&
        checkpoint.FlushedAtUtc >= latestOwnerProofAtUtc;

    private static bool MatchesReceipt(
        TenantTerminationTerminalReceipt receipt,
        TenantTerminationProcess process,
        CompleteTenantTerminationVerificationCommand command,
        TenantTerminationVerificationPlan plan,
        TenantTerminationOwnerWorkItem terminalOwner) =>
        receipt.Id == TenantTerminationExecutionIdentity.CreateTerminalReceiptId(
            process.Id,
            process.OperationRevision) &&
        receipt.IdempotencyKey == TenantTerminationExecutionIdentity
            .CreateTerminalReceiptIdempotencyKey(
                process.Id,
                process.OperationRevision) &&
        string.Equals(receipt.ScopeId, process.ScopeId, StringComparison.Ordinal) &&
        receipt.ProcessId == process.Id &&
        receipt.CaseId == process.CaseId &&
        receipt.ApprovalRevision == process.ApprovalRevision &&
        receipt.TerminationEpoch == process.TerminationEpoch &&
        receipt.DestroyOperationRevision == command.DestroyOperationRevision &&
        receipt.VerificationOperationRevision ==
            command.VerificationOperationRevision &&
        receipt.VerificationTaskRunId == command.TaskRunId &&
        receipt.VerificationTaskAttempt == command.TaskAttempt &&
        string.Equals(
            receipt.PolicyEvidenceSha256,
            process.PolicyEvidenceSha256,
            StringComparison.Ordinal) &&
        string.Equals(
            receipt.FrozenRevisionSha256,
            process.FrozenRevisionSha256,
            StringComparison.Ordinal) &&
        receipt.ExportRequested == process.ExportRequested &&
        receipt.ExportArtifactId == process.ExportArtifactId &&
        receipt.ExportArtifactVersion == process.ExportArtifactVersion &&
        string.Equals(
            receipt.ExportFragmentSetSha256,
            process.ExportFragmentSetSha256,
            StringComparison.Ordinal) &&
        receipt.OwnerCount == plan.WorkItems.Count &&
        string.Equals(
            receipt.OwnerProofSetSha256,
            plan.OwnerProofSetSha256,
            StringComparison.Ordinal) &&
        string.Equals(
            receipt.TerminalOwnerKey,
            plan.TerminalOwnerKey,
            StringComparison.Ordinal) &&
        receipt.TerminalOwnerSelectedProofRevision ==
            terminalOwner.SelectedProofRevision &&
        receipt.TerminalOwnerResultingProofRevision ==
            terminalOwner.ResultingProofRevision &&
        receipt.ReplayCheckpointSequence ==
            command.ReplayCheckpoint.Cursor.Sequence &&
        string.Equals(
            receipt.ReplayCheckpointRecordSha256,
            command.ReplayCheckpoint.Cursor.RecordSha256,
            StringComparison.Ordinal) &&
        receipt.ReplayIntegrityKeyVersion ==
            command.ReplayCheckpoint.IntegrityKeyVersion &&
        string.Equals(
            receipt.ReplayCheckpointProofSha256,
            command.ReplayCheckpoint.CheckpointProofSha256,
            StringComparison.Ordinal) &&
        receipt.ReplayCheckpointFlushedAtUtc ==
            command.ReplayCheckpoint.FlushedAtUtc;

    private static Result<TenantTerminationVerificationCompleted> Invalid() =>
        Result.Failure<TenantTerminationVerificationCompleted>(
            DataRightsApplicationErrors
                .TenantTerminationVerificationProofInvalid);
}

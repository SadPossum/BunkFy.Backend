namespace BunkFy.Modules.DataRights.Application.Handlers;

using BunkFy.Modules.DataRights.Application.Commands;
using BunkFy.Modules.DataRights.Application.Models;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Models;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;

internal sealed class CompleteTenantTerminationExportFragmentGenerationCommandHandler(
    ITenantTerminationRepository repository,
    ITenantTerminationExportFragmentRepository fragments,
    ITenantTerminationReplayStore replayStore,
    IRequestDispatcher dispatcher)
    : ICommandHandler<
        CompleteTenantTerminationExportFragmentGenerationCommand,
        TenantTerminationOwnerResultRecorded>
{
    public async Task<Result<TenantTerminationOwnerResultRecorded>> HandleAsync(
        CompleteTenantTerminationExportFragmentGenerationCommand command,
        CancellationToken cancellationToken)
    {
        TenantTerminationProcess? process = await repository.GetProcessAsync(
            command.ProcessId,
            cancellationToken).ConfigureAwait(false);
        TenantTerminationExportFragment? fragment =
            await fragments.GetAsync(
                command.WorkItemId,
                cancellationToken).ConfigureAwait(false);
        if (!IsCurrent(process, fragment, command))
        {
            return Invalid();
        }

        TenantTerminationReplayAttempt? replay =
            await replayStore.ReadAttemptAsync(
                new(
                    process!.ScopeId,
                    process.Id,
                    command.WorkItemId,
                    command.TaskRunId,
                    command.TaskAttempt),
                cancellationToken).ConfigureAwait(false);
        TenantTerminationContributionResult? contribution =
            replay?.Result?.Contribution;
        if (!Matches(
                process,
                fragment!,
                command,
                contribution))
        {
            return Invalid();
        }

        TenantTerminationProtectedExportFragment protectedFragment =
            command.ProtectedFragment;
        TenantTerminationExportOwnerResult owner =
            protectedFragment.AssemblyResult.Owner;
        Result available = fragment!.MarkAvailable(
            command.TaskRunId,
            command.TaskAttempt,
            owner.RecordCount,
            owner.SelectedProofRevision,
            owner.ResultingProofRevision,
            owner.ResultCode,
            contribution!.CatalogVersion,
            contribution.CatalogSha256,
            protectedFragment.StorageKey,
            protectedFragment.EncryptedByteLength,
            protectedFragment.PlaintextSha256,
            protectedFragment.EncryptionKeyVersion,
            protectedFragment.FormatVersion,
            protectedFragment.AvailableAtUtc,
            protectedFragment.ExpiresAtUtc);
        if (available.IsFailure)
        {
            return Result.Failure<TenantTerminationOwnerResultRecorded>(
                available.Error);
        }

        return await dispatcher.SendAsync<TenantTerminationOwnerResultRecorded>(
            new RecordTenantTerminationOwnerResultCommand(
                command.ProcessId,
                command.WorkItemId,
                command.OperationRevision,
                TenantTerminationContributionPhase.Export,
                command.OwnerKey,
                command.TaskRunId,
                command.TaskAttempt,
                command.ExpectedWorkItemVersion),
            cancellationToken).ConfigureAwait(false);
    }

    private static bool IsCurrent(
        TenantTerminationProcess? process,
        TenantTerminationExportFragment? fragment,
        CompleteTenantTerminationExportFragmentGenerationCommand command) =>
        process is not null &&
        fragment is not null &&
        command.ProtectedFragment is not null &&
        command.ExpectedWorkItemVersion > 0 &&
        command.ExpectedFragmentVersion > 0 &&
        process.Phase == TenantTerminationProcessPhase.Export &&
        process.Status == TenantTerminationProcessStatus.Running &&
        process.OperationRevision == command.OperationRevision &&
        fragment.Id == command.WorkItemId &&
        fragment.Version == command.ExpectedFragmentVersion &&
        fragment.State == TenantTerminationExportFragmentState.Generating &&
        fragment.ProcessId == process.Id &&
        fragment.ExportOperationRevision == process.OperationRevision &&
        fragment.GenerationRunId == command.TaskRunId &&
        fragment.GenerationAttempt == command.TaskAttempt &&
        string.Equals(
            fragment.OwnerKey,
            command.OwnerKey?.Trim(),
            StringComparison.Ordinal);

    private static bool Matches(
        TenantTerminationProcess process,
        TenantTerminationExportFragment fragment,
        CompleteTenantTerminationExportFragmentGenerationCommand command,
        TenantTerminationContributionResult? contribution)
    {
        TenantTerminationProtectedExportFragment? protectedFragment =
            command.ProtectedFragment;
        TenantTerminationExportFragmentAssemblyResult? assembly =
            protectedFragment?.AssemblyResult;
        TenantTerminationExportOwnerResult? owner = assembly?.Owner;
        return contribution is not null &&
            protectedFragment is not null &&
            assembly is not null &&
            owner is not null &&
            contribution.Status ==
                TenantTerminationContributionStatus.Completed &&
            string.Equals(
                assembly.FrozenRevisionSha256,
                process.FrozenRevisionSha256,
                StringComparison.Ordinal) &&
            string.Equals(owner.OwnerKey, fragment.OwnerKey, StringComparison.Ordinal) &&
            owner.RecordCount == contribution.AffectedCount &&
            contribution.RetainedMinimumCount == 0 &&
            contribution.RemainingActiveCount == 0 &&
            !contribution.HoldReviewAtUtc.HasValue &&
            owner.SelectedProofRevision ==
                contribution.SelectedProofRevision &&
            owner.ResultingProofRevision ==
                contribution.ResultingProofRevision &&
            string.Equals(
                owner.ResultCode,
                contribution.ResultCode,
                StringComparison.Ordinal) &&
            owner.RecordedAtUtc == contribution.RecordedAtUtc &&
            contribution.CatalogVersion == fragment.CatalogVersion &&
            string.Equals(
                contribution.CatalogSha256,
                fragment.CatalogSha256,
                StringComparison.Ordinal) &&
            protectedFragment.ExpiresAtUtc == fragment.ExpiresAtUtc &&
            protectedFragment.AvailableAtUtc >= owner.RecordedAtUtc;
    }

    private static Result<TenantTerminationOwnerResultRecorded> Invalid() =>
        Result.Failure<TenantTerminationOwnerResultRecorded>(
            DataRightsApplicationErrors
                .TenantTerminationExecutionStateInvalid);
}

namespace BunkFy.Modules.DataRights.Application.Handlers;

using BunkFy.Modules.DataRights.Application.Commands;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Models;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;

internal sealed class PrepareTenantTerminationVerificationCommandHandler(
    ITenantTerminationRepository repository,
    TenantTerminationMutationCoordinator mutations,
    ITenantTerminationTerminalReceiptRepository receipts,
    TenantTerminationVerificationPlanner planner)
    : ICommandHandler<
        PrepareTenantTerminationVerificationCommand,
        TenantTerminationVerificationStart>
{
    public async Task<Result<TenantTerminationVerificationStart>> HandleAsync(
        PrepareTenantTerminationVerificationCommand command,
        CancellationToken cancellationToken)
    {
        if (command.ProcessId == Guid.Empty ||
            command.VerificationOperationRevision <= 0 ||
            command.TaskRunId != TenantTerminationExecutionIdentity
                .CreateVerificationTaskRunId(
                    command.ProcessId,
                    command.VerificationOperationRevision) ||
            command.TaskAttempt <= 0)
        {
            return Invalid();
        }

        TenantTerminationProcess? process =
            await mutations.AcquireProcessReadAsync(
            command.ProcessId,
            cancellationToken).ConfigureAwait(false);
        if (process is null)
        {
            return Result.Failure<TenantTerminationVerificationStart>(
                DataRightsApplicationErrors.TenantTerminationProcessNotFound);
        }

        if (process.Phase == TenantTerminationProcessPhase.Completed &&
            process.Status == TenantTerminationProcessStatus.Completed)
        {
            return await this.PrepareCompletedReplayAsync(
                process,
                command,
                cancellationToken).ConfigureAwait(false);
        }

        if (process.Phase != TenantTerminationProcessPhase.Verify ||
            process.Status != TenantTerminationProcessStatus.Running ||
            process.OperationRevision !=
                command.VerificationOperationRevision ||
            process.DestroyCompletedOperationRevision is not long
                destroyOperationRevision)
        {
            return Invalid();
        }

        IReadOnlyList<TenantTerminationOwnerWorkItem> workItems =
            await repository.ListOwnerWorkItemsAsync(
                process.Id,
                TenantTerminationOwnerPhase.Destroy,
                destroyOperationRevision,
                cancellationToken).ConfigureAwait(false);
        Result<TenantTerminationVerificationPlan> prepared = planner.Prepare(
            process,
            workItems);
        return prepared.IsSuccess
            ? Result.Success(new TenantTerminationVerificationStart(
                DispatchRequired: true,
                process.Version,
                prepared.Value.DestroyOperationRevision,
                prepared.Value.VerificationOperationRevision,
                prepared.Value.OwnerProofSetSha256,
                prepared.Value.TerminalOwnerKey,
                prepared.Value.WorkItems))
            : Result.Failure<TenantTerminationVerificationStart>(
                prepared.Error);
    }

    private async Task<Result<TenantTerminationVerificationStart>>
        PrepareCompletedReplayAsync(
            TenantTerminationProcess process,
            PrepareTenantTerminationVerificationCommand command,
            CancellationToken cancellationToken)
    {
        if (process.OperationRevision !=
                command.VerificationOperationRevision ||
            !process.HasCurrentVerificationConfirmation() ||
            process.DestroyCompletedOperationRevision is not long
                destroyOperationRevision ||
            process.TerminalReceiptId is not Guid receiptId)
        {
            return Invalid();
        }

        TenantTerminationTerminalReceipt? receipt = await receipts.GetAsync(
            receiptId,
            cancellationToken).ConfigureAwait(false);
        if (receipt is null ||
            receipt.ProcessId != process.Id ||
            receipt.DestroyOperationRevision != destroyOperationRevision ||
            receipt.VerificationOperationRevision !=
                command.VerificationOperationRevision ||
            process.TerminalReceiptVersion != receipt.Version ||
            !string.Equals(
                process.VerificationOwnerProofSetSha256,
                receipt.OwnerProofSetSha256,
                StringComparison.Ordinal))
        {
            return Invalid();
        }

        return Result.Success(new TenantTerminationVerificationStart(
            DispatchRequired: false,
            process.Version,
            destroyOperationRevision,
            process.OperationRevision,
            receipt.OwnerProofSetSha256,
            receipt.TerminalOwnerKey,
            WorkItems: []));
    }

    private static Result<TenantTerminationVerificationStart> Invalid() =>
        Result.Failure<TenantTerminationVerificationStart>(
            DataRightsApplicationErrors
                .TenantTerminationVerificationProofInvalid);
}

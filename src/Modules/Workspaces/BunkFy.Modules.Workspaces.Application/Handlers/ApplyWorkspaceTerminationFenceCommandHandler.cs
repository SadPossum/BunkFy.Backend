namespace BunkFy.Modules.Workspaces.Application.Handlers;

using BunkFy.Modules.Workspaces.Application.Commands;
using BunkFy.Modules.Workspaces.Application.Mapping;
using BunkFy.Modules.Workspaces.Application.Ports;
using BunkFy.Modules.Workspaces.Contracts;
using BunkFy.Modules.Workspaces.Domain.Termination;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;

internal sealed class ApplyWorkspaceTerminationFenceCommandHandler(
    IWorkspaceTerminationFenceRepository fences,
    IScopeContext scopeContext,
    ISystemClock clock,
    IIdGenerator ids)
    : ICommandHandler<
        ApplyWorkspaceTerminationFenceCommand,
        WorkspaceTerminationFenceReceiptDto>
{
    public async Task<Result<WorkspaceTerminationFenceReceiptDto>> HandleAsync(
        ApplyWorkspaceTerminationFenceCommand command,
        CancellationToken cancellationToken)
    {
        if (!scopeContext.IsEnabled ||
            string.IsNullOrWhiteSpace(scopeContext.ScopeId))
        {
            return WorkspaceTerminationFenceHandlerSupport
                .Failure<WorkspaceTerminationFenceReceiptDto>(
                    WorkspaceTerminationApplicationErrors.ScopeRequired);
        }

        if (!WorkspaceTerminationFenceHandlerSupport.HasValidCoordinates(
                command.IdempotencyKey,
                command.ProcessId,
                command.CaseId,
                command.ApprovalRevision,
                command.OperationRevision,
                command.WorkItemId,
                command.TerminationEpoch,
                command.PolicyEvidenceSha256,
                command.ActorId))
        {
            return WorkspaceTerminationFenceHandlerSupport
                .Failure<WorkspaceTerminationFenceReceiptDto>(
                    WorkspaceTerminationApplicationErrors.RequestInvalid);
        }

        WorkspaceTerminationFenceReceipt? replay =
            await fences.FindReceiptAsync(
                command.IdempotencyKey,
                cancellationToken).ConfigureAwait(false);
        if (replay is not null)
        {
            return Replay(replay, command);
        }

        WorkspaceTerminationFence? active = await fences.GetActiveAsync(
            cancellationToken).ConfigureAwait(false);
        if (active is not null)
        {
            return WorkspaceTerminationFenceHandlerSupport
                .Failure<WorkspaceTerminationFenceReceiptDto>(
                    WorkspaceTerminationApplicationErrors
                        .ActiveFenceConflict);
        }

        if (await fences.HasCoordinatesAsync(
                command.ProcessId,
                command.TerminationEpoch,
                cancellationToken).ConfigureAwait(false))
        {
            return WorkspaceTerminationFenceHandlerSupport
                .Failure<WorkspaceTerminationFenceReceiptDto>(
                    WorkspaceTerminationApplicationErrors
                        .FenceCoordinatesConflict);
        }

        DateTimeOffset nowUtc =
            WorkspaceTerminationFenceHandlerSupport.ToPersistencePrecision(
                clock.UtcNow);
        Result<WorkspaceTerminationFence> created =
            WorkspaceTerminationFence.Freeze(
                ids.NewId(),
                scopeContext.ScopeId,
                command.ProcessId,
                command.CaseId,
                command.ApprovalRevision,
                command.TerminationEpoch,
                command.PolicyEvidenceSha256,
                command.ActorId,
                nowUtc);
        if (created.IsFailure)
        {
            return WorkspaceTerminationFenceHandlerSupport
                .Failure<WorkspaceTerminationFenceReceiptDto>(created.Error);
        }

        Result<WorkspaceTerminationFenceReceipt> receipt =
            WorkspaceTerminationFenceReceipt.Create(
                ids.NewId(),
                scopeContext.ScopeId,
                created.Value.Id,
                command.ProcessId,
                command.CaseId,
                command.ApprovalRevision,
                command.OperationRevision,
                command.WorkItemId,
                command.IdempotencyKey,
                command.TerminationEpoch,
                WorkspaceTerminationFenceAction.Freeze,
                selectedFenceVersion: 0,
                created.Value.Version,
                created.Value.State,
                command.PolicyEvidenceSha256,
                command.ActorId,
                nowUtc);
        if (receipt.IsFailure)
        {
            return WorkspaceTerminationFenceHandlerSupport
                .Failure<WorkspaceTerminationFenceReceiptDto>(receipt.Error);
        }

        await fences.AddAsync(
            created.Value,
            cancellationToken).ConfigureAwait(false);
        await fences.AddReceiptAsync(
            receipt.Value,
            cancellationToken).ConfigureAwait(false);
        return Result.Success(receipt.Value.ToDto());
    }

    private static Result<WorkspaceTerminationFenceReceiptDto> Replay(
        WorkspaceTerminationFenceReceipt receipt,
        ApplyWorkspaceTerminationFenceCommand command)
    {
        if (receipt.Action != WorkspaceTerminationFenceAction.Freeze ||
            receipt.ProcessId != command.ProcessId ||
            receipt.CaseId != command.CaseId ||
            receipt.ApprovalRevision != command.ApprovalRevision ||
            receipt.OperationRevision != command.OperationRevision ||
            receipt.WorkItemId != command.WorkItemId ||
            receipt.TerminationEpoch != command.TerminationEpoch ||
            receipt.SelectedFenceVersion != 0 ||
            !string.Equals(
                receipt.PolicyEvidenceSha256,
                command.PolicyEvidenceSha256,
                StringComparison.Ordinal) ||
            !string.Equals(
                receipt.ActorId,
                command.ActorId.Trim(),
                StringComparison.Ordinal))
        {
            return WorkspaceTerminationFenceHandlerSupport
                .Failure<WorkspaceTerminationFenceReceiptDto>(
                    WorkspaceTerminationApplicationErrors
                        .IdempotencyConflict);
        }

        return Result.Success(receipt.ToDto());
    }
}

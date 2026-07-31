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

internal sealed class ReleaseWorkspaceTerminationFenceCommandHandler(
    IWorkspaceTerminationFenceRepository fences,
    IScopeContext scopeContext,
    ISystemClock clock,
    IIdGenerator ids)
    : ICommandHandler<
        ReleaseWorkspaceTerminationFenceCommand,
        WorkspaceTerminationFenceReceiptDto>
{
    public async Task<Result<WorkspaceTerminationFenceReceiptDto>> HandleAsync(
        ReleaseWorkspaceTerminationFenceCommand command,
        CancellationToken cancellationToken)
    {
        Result? validation = Validate(command, scopeContext);
        if (validation?.IsFailure == true)
        {
            return WorkspaceTerminationFenceHandlerSupport
                .Failure<WorkspaceTerminationFenceReceiptDto>(
                    validation.Error);
        }

        WorkspaceTerminationFenceReceipt? replay =
            await fences.FindReceiptAsync(
                command.IdempotencyKey,
                cancellationToken).ConfigureAwait(false);
        if (replay is not null)
        {
            return Replay(replay, command);
        }

        WorkspaceTerminationFence? fence = await fences.GetByProcessAsync(
            command.ProcessId,
            cancellationToken).ConfigureAwait(false);
        if (fence is null ||
            !await fences.TryLockAsync(
                fence.Id,
                cancellationToken).ConfigureAwait(false))
        {
            return WorkspaceTerminationFenceHandlerSupport
                .Failure<WorkspaceTerminationFenceReceiptDto>(
                    WorkspaceTerminationApplicationErrors.FenceNotFound);
        }

        replay = await fences.FindReceiptAsync(
            command.IdempotencyKey,
            cancellationToken).ConfigureAwait(false);
        if (replay is not null)
        {
            return Replay(replay, command);
        }

        fence = await fences.GetByProcessAsync(
            command.ProcessId,
            cancellationToken).ConfigureAwait(false);
        if (fence is null ||
            !WorkspaceTerminationFenceHandlerSupport.MatchesFence(
                fence,
                command.ProcessId,
                command.CaseId,
                command.ApprovalRevision,
                command.TerminationEpoch,
                command.PolicyEvidenceSha256))
        {
            return WorkspaceTerminationFenceHandlerSupport
                .Failure<WorkspaceTerminationFenceReceiptDto>(
                    WorkspaceTerminationApplicationErrors
                        .FenceCoordinatesConflict);
        }

        long selectedVersion = fence.Version;
        DateTimeOffset nowUtc =
            WorkspaceTerminationFenceHandlerSupport.ToPersistencePrecision(
                clock.UtcNow);
        Result released = fence.Release(
            command.ExpectedFenceVersion,
            command.ActorId,
            nowUtc);
        if (released.IsFailure)
        {
            return WorkspaceTerminationFenceHandlerSupport
                .Failure<WorkspaceTerminationFenceReceiptDto>(released.Error);
        }

        Result<WorkspaceTerminationFenceReceipt> receipt =
            WorkspaceTerminationFenceReceipt.Create(
                ids.NewId(),
                scopeContext.ScopeId!,
                fence.Id,
                command.ProcessId,
                command.CaseId,
                command.ApprovalRevision,
                command.OperationRevision,
                command.WorkItemId,
                command.IdempotencyKey,
                command.TerminationEpoch,
                WorkspaceTerminationFenceAction.Release,
                selectedVersion,
                fence.Version,
                fence.State,
                command.PolicyEvidenceSha256,
                command.ActorId,
                nowUtc);
        if (receipt.IsFailure)
        {
            return WorkspaceTerminationFenceHandlerSupport
                .Failure<WorkspaceTerminationFenceReceiptDto>(receipt.Error);
        }

        await fences.AddReceiptAsync(
            receipt.Value,
            cancellationToken).ConfigureAwait(false);
        return Result.Success(receipt.Value.ToDto());
    }

    private static Result? Validate(
        ReleaseWorkspaceTerminationFenceCommand command,
        IScopeContext scopeContext)
    {
        if (!scopeContext.IsEnabled ||
            string.IsNullOrWhiteSpace(scopeContext.ScopeId))
        {
            return Result.Failure(
                WorkspaceTerminationApplicationErrors.ScopeRequired);
        }

        return command.ExpectedFenceVersion < 1 ||
            !WorkspaceTerminationFenceHandlerSupport.HasValidCoordinates(
                command.IdempotencyKey,
                command.ProcessId,
                command.CaseId,
                command.ApprovalRevision,
                command.OperationRevision,
                command.WorkItemId,
                command.TerminationEpoch,
                command.PolicyEvidenceSha256,
                command.ActorId)
            ? Result.Failure(
                WorkspaceTerminationApplicationErrors.RequestInvalid)
            : null;
    }

    private static Result<WorkspaceTerminationFenceReceiptDto> Replay(
        WorkspaceTerminationFenceReceipt receipt,
        ReleaseWorkspaceTerminationFenceCommand command)
    {
        if (receipt.Action != WorkspaceTerminationFenceAction.Release ||
            receipt.ProcessId != command.ProcessId ||
            receipt.CaseId != command.CaseId ||
            receipt.ApprovalRevision != command.ApprovalRevision ||
            receipt.OperationRevision != command.OperationRevision ||
            receipt.WorkItemId != command.WorkItemId ||
            receipt.TerminationEpoch != command.TerminationEpoch ||
            receipt.SelectedFenceVersion != command.ExpectedFenceVersion ||
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

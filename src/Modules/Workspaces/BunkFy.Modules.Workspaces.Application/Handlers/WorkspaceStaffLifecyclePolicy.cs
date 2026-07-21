namespace BunkFy.Modules.Workspaces.Application.Handlers;

using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Workspaces.Application.Commands;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Microsoft.Extensions.Logging;

internal sealed class WorkspaceStaffLifecyclePolicy(
    IRequestDispatcher dispatcher,
    ILogger<WorkspaceStaffLifecyclePolicy> logger)
    : IStaffLifecyclePolicy
{
    public async ValueTask<StaffLifecyclePolicyDecision> PrepareAsync(
        StaffLifecyclePolicyContext context,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(context.AuthSubjectId))
        {
            return StaffLifecyclePolicyDecision.Allowed;
        }

        try
        {
            Result<WorkspaceStaffAccessPreparation> prepared = await dispatcher.SendAsync(
                new PrepareWorkspaceStaffAccessCommand(context),
                cancellationToken).ConfigureAwait(false);
            if (prepared.IsFailure)
            {
                logger.LogWarning(
                    "Workspace access preparation rejected Staff transition {TransitionId} with {ErrorCode}.",
                    context.TransitionId,
                    prepared.Error.Code);
                return StaffLifecyclePolicyDecision.RetryRequired;
            }

            if (!prepared.Value.RequiresAccessDenial)
            {
                return StaffLifecyclePolicyDecision.Allowed;
            }

            Result<WorkspaceStaffAccessCoordinationOutcome> denied = await dispatcher.SendAsync(
                new DenyWorkspaceStaffAccessCommand(prepared.Value.ProcessId),
                cancellationToken).ConfigureAwait(false);
            if (denied.IsFailure)
            {
                logger.LogWarning(
                    "Workspace access denial rejected Staff transition {TransitionId} with {ErrorCode}.",
                    context.TransitionId,
                    denied.Error.Code);
                return StaffLifecyclePolicyDecision.RetryRequired;
            }

            if (denied.Value == WorkspaceStaffAccessCoordinationOutcome.RetryRequired)
            {
                logger.LogWarning(
                    "Workspace access denial for Staff transition {TransitionId} requires retry.",
                    context.TransitionId);
            }

            return denied.Value switch
            {
                WorkspaceStaffAccessCoordinationOutcome.Allowed => StaffLifecyclePolicyDecision.Allowed,
                WorkspaceStaffAccessCoordinationOutcome.OwnerProtected =>
                    StaffLifecyclePolicyDecision.OwnerProtected,
                _ => StaffLifecyclePolicyDecision.RetryRequired
            };
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(
                exception,
                "Workspace access could not prepare Staff transition {TransitionId}.",
                context.TransitionId);
            return StaffLifecyclePolicyDecision.RetryRequired;
        }
    }
}

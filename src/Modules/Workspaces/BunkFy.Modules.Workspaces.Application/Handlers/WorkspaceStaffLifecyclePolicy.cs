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
                    "Workspace access preparation rejected a Staff transition with {ErrorCode}.",
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
                    "Workspace access denial rejected a Staff transition with {ErrorCode}.",
                    denied.Error.Code);
                return StaffLifecyclePolicyDecision.RetryRequired;
            }

            if (denied.Value == WorkspaceStaffAccessCoordinationOutcome.RetryRequired)
            {
                logger.LogWarning("Workspace access denial for a Staff transition requires retry.");
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
                "Workspace access could not prepare a Staff transition because {ExceptionType} was raised.",
                exception.GetType().Name);
            return StaffLifecyclePolicyDecision.RetryRequired;
        }
    }
}

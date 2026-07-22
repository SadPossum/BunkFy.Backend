namespace BunkFy.Modules.Workspaces.Application.Handlers;

using BunkFy.Modules.Workspaces.Application.Commands;
using BunkFy.Modules.Workspaces.Domain;
using Gma.Framework.Runtime.Time;
using Gma.Modules.Organizations.Contracts;
using Microsoft.Extensions.Logging;

internal sealed class WorkspaceStaffAccessDenier(
    IOrganizationMembershipLifecycle memberships,
    WorkspaceAccessProvisioner access,
    ISystemClock clock,
    ILogger<WorkspaceStaffAccessDenier> logger)
{
    public async Task<WorkspaceStaffAccessCoordinationOutcome> DenyAsync(
        WorkspaceStaffAccessProcess process,
        CancellationToken cancellationToken)
    {
        if (process.State == WorkspaceStaffAccessProcessState.AwaitingStaffCommit)
        {
            return WorkspaceStaffAccessCoordinationOutcome.Allowed;
        }

        if (process.State != WorkspaceStaffAccessProcessState.Prepared ||
            process.TargetState == WorkspaceStaffAccessTargetState.Active)
        {
            return WorkspaceStaffAccessCoordinationOutcome.RetryRequired;
        }

        OrganizationMembershipStatus desiredStatus = process.TargetState ==
            WorkspaceStaffAccessTargetState.Departed
            ? OrganizationMembershipStatus.Removed
            : OrganizationMembershipStatus.Suspended;
        try
        {
            OrganizationMembershipLifecycleResult membership = await memberships.EnsureStateAsync(
                Guid.Parse(process.ScopeId),
                process.SubjectId,
                desiredStatus,
                WorkspaceAccessProvisioner.ProvisioningActorId,
                cancellationToken).ConfigureAwait(false);
            if (membership.Outcome == OrganizationMembershipLifecycleOutcome.OwnerProtected)
            {
                process.RecordFailure("Workspaces.StaffAccessOwnerProtected", clock.UtcNow);
                return WorkspaceStaffAccessCoordinationOutcome.OwnerProtected;
            }

            if (membership.Outcome is OrganizationMembershipLifecycleOutcome.Unknown or
                OrganizationMembershipLifecycleOutcome.TransitionNotAllowed)
            {
                process.RecordFailure("Workspaces.OrganizationMembershipTransitionFailed", clock.UtcNow);
                return WorkspaceStaffAccessCoordinationOutcome.RetryRequired;
            }

            await access.DenyMemberAsync(
                process.ScopeId,
                process.SubjectId,
                cancellationToken).ConfigureAwait(false);
            return process.MarkAwaitingStaffCommit(clock.UtcNow).IsSuccess
                ? WorkspaceStaffAccessCoordinationOutcome.Allowed
                : WorkspaceStaffAccessCoordinationOutcome.RetryRequired;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            process.RecordFailure("Workspaces.StaffAccessDenialFailed", clock.UtcNow);
            logger.LogWarning(
                "A workspace Staff access process could not deny access because {ExceptionType} was raised.",
                exception.GetType().Name);
            return WorkspaceStaffAccessCoordinationOutcome.RetryRequired;
        }
    }
}

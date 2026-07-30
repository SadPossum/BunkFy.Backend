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

        WorkspaceStaffAccessCoordinationOutcome outcome =
            await this.EnsureAccessDeniedAsync(
                process.ScopeId,
                process.SubjectId,
                process.TargetState,
                cancellationToken).ConfigureAwait(false);
        if (outcome == WorkspaceStaffAccessCoordinationOutcome.OwnerProtected)
        {
            process.RecordFailure(
                "Workspaces.StaffAccessOwnerProtected",
                clock.UtcNow);
            return outcome;
        }

        if (outcome != WorkspaceStaffAccessCoordinationOutcome.Allowed)
        {
            process.RecordFailure(
                "Workspaces.StaffAccessDenialFailed",
                clock.UtcNow);
            return outcome;
        }

        return process.MarkAwaitingStaffCommit(clock.UtcNow).IsSuccess
            ? WorkspaceStaffAccessCoordinationOutcome.Allowed
            : WorkspaceStaffAccessCoordinationOutcome.RetryRequired;
    }

    public async Task<WorkspaceStaffAccessCoordinationOutcome>
        EnsureAccessDeniedAsync(
            string workspaceId,
            string subjectId,
            WorkspaceStaffAccessTargetState targetState,
            CancellationToken cancellationToken)
    {
        if (targetState is not (
                WorkspaceStaffAccessTargetState.Suspended or
                WorkspaceStaffAccessTargetState.Departed) ||
            !Guid.TryParse(workspaceId, out Guid organizationId) ||
            string.IsNullOrWhiteSpace(subjectId))
        {
            return WorkspaceStaffAccessCoordinationOutcome.RetryRequired;
        }

        OrganizationMembershipStatus desiredStatus = targetState ==
            WorkspaceStaffAccessTargetState.Departed
            ? OrganizationMembershipStatus.Removed
            : OrganizationMembershipStatus.Suspended;
        try
        {
            OrganizationMembershipLifecycleResult membership =
                await memberships.EnsureStateAsync(
                    organizationId,
                    subjectId.Trim(),
                    desiredStatus,
                    WorkspaceAccessProvisioner.ProvisioningActorId,
                    cancellationToken).ConfigureAwait(false);
            if (membership.Outcome ==
                OrganizationMembershipLifecycleOutcome.OwnerProtected)
            {
                return WorkspaceStaffAccessCoordinationOutcome.OwnerProtected;
            }

            if (membership.Outcome is not (
                    OrganizationMembershipLifecycleOutcome.Changed or
                    OrganizationMembershipLifecycleOutcome
                        .AlreadyInDesiredState or
                    OrganizationMembershipLifecycleOutcome.NotFound))
            {
                return WorkspaceStaffAccessCoordinationOutcome.RetryRequired;
            }

            await access.DenyMemberAsync(
                workspaceId,
                subjectId.Trim(),
                cancellationToken).ConfigureAwait(false);
            return WorkspaceStaffAccessCoordinationOutcome.Allowed;
        }
        catch (Exception exception)
            when (exception is not OperationCanceledException)
        {
            logger.LogWarning(
                "Workspace Staff access denial failed because {ExceptionType} was raised.",
                exception.GetType().Name);
            return WorkspaceStaffAccessCoordinationOutcome.RetryRequired;
        }
    }
}

namespace BunkFy.Modules.Workspaces.Application.Handlers;

using BunkFy.Modules.Workspaces.Application.Commands;
using BunkFy.Modules.Workspaces.Domain;
using Gma.Framework.Runtime.Time;
using Gma.Modules.Organizations.Contracts;
using Microsoft.Extensions.Logging;

internal sealed class WorkspaceStaffAccessRestorer(
    IOrganizationMembershipLifecycle memberships,
    WorkspaceAccessProvisioner access,
    ISystemClock clock,
    ILogger<WorkspaceStaffAccessRestorer> logger)
{
    public async Task<WorkspaceStaffAccessCoordinationOutcome> RestoreAsync(
        WorkspaceStaffAccessProcess process,
        CancellationToken cancellationToken)
    {
        if (process.State == WorkspaceStaffAccessProcessState.Completed)
        {
            return WorkspaceStaffAccessCoordinationOutcome.Allowed;
        }

        if (process.State != WorkspaceStaffAccessProcessState.RestorationPending ||
            process.TargetState != WorkspaceStaffAccessTargetState.Active)
        {
            return WorkspaceStaffAccessCoordinationOutcome.RetryRequired;
        }

        try
        {
            OrganizationMembershipLifecycleResult membership = await memberships.EnsureStateAsync(
                Guid.Parse(process.ScopeId),
                process.SubjectId,
                OrganizationMembershipStatus.Active,
                WorkspaceAccessProvisioner.ProvisioningActorId,
                cancellationToken).ConfigureAwait(false);
            if (membership.Outcome == OrganizationMembershipLifecycleOutcome.OwnerProtected)
            {
                process.RecordFailure("Workspaces.StaffAccessOwnerProtected", clock.UtcNow);
                return WorkspaceStaffAccessCoordinationOutcome.OwnerProtected;
            }

            if (membership.Outcome is not (OrganizationMembershipLifecycleOutcome.Changed or
                OrganizationMembershipLifecycleOutcome.AlreadyInDesiredState))
            {
                process.RecordFailure("Workspaces.OrganizationMembershipRestoreFailed", clock.UtcNow);
                return WorkspaceStaffAccessCoordinationOutcome.RetryRequired;
            }

            await access.RestoreMemberAsync(
                process.ScopeId,
                process.SubjectId,
                process.ProfileSnapshots
                    .Select(snapshot => new WorkspaceStaffAccessProfileTarget(
                        snapshot.ProfileId,
                        snapshot.AssignmentScope))
                    .ToArray(),
                cancellationToken).ConfigureAwait(false);
            return process.Complete(clock.UtcNow).IsSuccess
                ? WorkspaceStaffAccessCoordinationOutcome.Allowed
                : WorkspaceStaffAccessCoordinationOutcome.RetryRequired;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            process.RecordFailure("Workspaces.StaffAccessRestoreFailed", clock.UtcNow);
            logger.LogWarning(
                "A workspace Staff access process could not restore access because {ExceptionType} was raised.",
                exception.GetType().Name);
            return WorkspaceStaffAccessCoordinationOutcome.RetryRequired;
        }
    }
}

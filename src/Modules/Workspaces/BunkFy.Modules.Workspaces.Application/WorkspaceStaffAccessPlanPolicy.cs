namespace BunkFy.Modules.Workspaces.Application;

using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Workspaces.Application.Ports;
using BunkFy.Modules.Workspaces.Contracts;
using BunkFy.Modules.Workspaces.Domain;
using Gma.Framework.AccessControl;
using Gma.Framework.Permissions;
using Gma.Framework.Results;
using Gma.Modules.AccessControl.Contracts;

internal sealed class WorkspaceStaffAccessPlanPolicy(
    IAccessProfileProvisioner profiles,
    IAccessControlRoleProvisioner roles,
    IAccessAuthorizationService authorization,
    IWorkspacePropertyProjectionRepository properties)
{
    private static readonly HashSet<string> DelegablePermissions =
        WorkspaceAccessRoles.DelegablePermissions.ToHashSet(StringComparer.Ordinal);
    private static readonly HashSet<string> ReusableProfileKeys =
        new(
            [
                WorkspaceAccessProfileSeeds.FrontDeskKey,
                WorkspaceAccessProfileSeeds.HousekeepingKey,
                WorkspaceAccessProfileSeeds.ViewerKey
            ],
            StringComparer.Ordinal);

    public async Task<Result<AccessProfileDto>> ValidateAsync(
        string workspaceId,
        WorkspaceStaffOnboardingSource sourceKind,
        string profileKey,
        IReadOnlyCollection<Guid> propertyIds,
        string actorSubjectId,
        CancellationToken cancellationToken)
    {
        AccessScope workspaceScope = WorkspaceAccessScopes.Create(workspaceId);
        AccessProfileDto? profile = await profiles.FindProfileByKeyAsync(
                workspaceScope,
                profileKey,
                cancellationToken)
            .ConfigureAwait(false);
        if (profile?.Status != AccessProfileStatus.Active)
        {
            return Result.Failure<AccessProfileDto>(
                WorkspaceStaffAccessPlanApplicationErrors.ProfileUnavailable);
        }

        if (profile.Permissions.Any(permission => !DelegablePermissions.Contains(permission)) ||
            (sourceKind == WorkspaceStaffOnboardingSource.EnrollmentLink &&
             !ReusableProfileKeys.Contains(profile.Key)))
        {
            return Result.Failure<AccessProfileDto>(
                WorkspaceStaffAccessPlanApplicationErrors.ProfileNotDelegable);
        }

        Guid[] distinctPropertyIds = propertyIds.Distinct().ToArray();
        if (!await properties.AreAllActiveAsync(distinctPropertyIds, cancellationToken)
                .ConfigureAwait(false))
        {
            return Result.Failure<AccessProfileDto>(
                WorkspaceStaffAccessPlanApplicationErrors.PropertyUnavailable);
        }

        AccessSubject actor = AccessSubject.User(actorSubjectId);
        if (await roles.HasAssignmentAsync(
                actor,
                WorkspaceAccessRoles.Owner,
                workspaceScope,
                cancellationToken).ConfigureAwait(false))
        {
            return Result.Success(profile);
        }

        AccessScope[] assignmentScopes = distinctPropertyIds.Length == 0
            ? [workspaceScope]
            : distinctPropertyIds
                .Select(propertyId => WorkspaceAccessScopes.CreateProperty(workspaceId, propertyId))
                .ToArray();
        PermissionCode[] requiredPermissions = profile.Permissions
            .Append(StaffAdminPermissionCodes.AssignProperties)
            .Distinct(StringComparer.Ordinal)
            .Select(PermissionCode.Create)
            .ToArray();
        AccessRequirement[] requirements = assignmentScopes
            .SelectMany(scope => requiredPermissions.Select(permission =>
                new AccessRequirement(actor, permission, scope)))
            .ToArray();
        IReadOnlyList<AccessDecision> decisions = await authorization.AuthorizeManyAsync(
                requirements,
                cancellationToken)
            .ConfigureAwait(false);
        return decisions.All(decision => decision.IsAllowed)
            ? Result.Success(profile)
            : Result.Failure<AccessProfileDto>(
                WorkspaceStaffAccessPlanApplicationErrors.DelegationDenied);
    }
}

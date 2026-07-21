namespace BunkFy.Modules.Workspaces.Tests;

using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Workspaces.Application;
using BunkFy.Modules.Workspaces.Application.Ports;
using BunkFy.Modules.Workspaces.Contracts;
using BunkFy.Modules.Workspaces.Domain;
using Gma.Framework.AccessControl;
using Gma.Modules.AccessControl.Contracts;
using Xunit;

[Trait("Category", "Unit")]
public sealed class WorkspaceStaffAccessPlanPolicyTests
{
    private static readonly string WorkspaceId = Guid.NewGuid().ToString("D");
    private static readonly AccessSubject Actor = AccessSubject.User("manager-a");

    [Fact]
    public async Task Owner_can_delegate_active_profile_to_active_properties_without_permission_fanout()
    {
        Guid propertyId = Guid.NewGuid();
        StubRoles roles = new(owner: true);
        StubAuthorization authorization = new(_ => AccessDecision.Denied("unexpected"));
        WorkspaceStaffAccessPlanPolicy policy = CreatePolicy(
            Profile(WorkspaceAccessProfileSeeds.FrontDeskKey),
            roles,
            authorization,
            propertiesActive: true);

        var result = await policy.ValidateAsync(
            WorkspaceId,
            WorkspaceStaffOnboardingSource.Invitation,
            WorkspaceAccessProfileSeeds.FrontDeskKey,
            [propertyId],
            Actor.Id,
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error.Code);
        Assert.Empty(authorization.Requirements);
    }

    [Fact]
    public async Task Delegated_manager_needs_profile_and_property_assignment_permissions_at_each_scope()
    {
        Guid propertyA = Guid.NewGuid();
        Guid propertyB = Guid.NewGuid();
        AccessProfileDto profile = Profile(
            WorkspaceAccessProfileSeeds.FrontDeskKey,
            ["reservations.read", "inventory.read"]);
        StubAuthorization authorization = new(_ => AccessDecision.Allowed());
        WorkspaceStaffAccessPlanPolicy policy = CreatePolicy(
            profile,
            new StubRoles(owner: false),
            authorization,
            propertiesActive: true);

        var result = await policy.ValidateAsync(
            WorkspaceId,
            WorkspaceStaffOnboardingSource.Invitation,
            profile.Key,
            [propertyA, propertyB],
            Actor.Id,
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error.Code);
        Assert.Equal(6, authorization.Requirements.Count);
        Assert.All(authorization.Requirements, requirement => Assert.Equal(Actor, requirement.Subject));
        Assert.Contains(authorization.Requirements, requirement =>
            requirement.Permission.Value == StaffAdminPermissionCodes.AssignProperties &&
            requirement.Scope.Value == WorkspaceAccessScopes.CreateProperty(WorkspaceId, propertyA).Value);
        Assert.Contains(authorization.Requirements, requirement =>
            requirement.Permission.Value == StaffAdminPermissionCodes.AssignProperties &&
            requirement.Scope.Value == WorkspaceAccessScopes.CreateProperty(WorkspaceId, propertyB).Value);
    }

    [Fact]
    public async Task Reusable_link_rejects_custom_profile_even_when_permissions_are_delegable()
    {
        AccessProfileDto profile = Profile("night-manager", ["reservations.read"]);
        WorkspaceStaffAccessPlanPolicy policy = CreatePolicy(
            profile,
            new StubRoles(owner: true),
            new StubAuthorization(_ => AccessDecision.Allowed()),
            propertiesActive: true);

        var result = await policy.ValidateAsync(
            WorkspaceId,
            WorkspaceStaffOnboardingSource.EnrollmentLink,
            profile.Key,
            [],
            Actor.Id,
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(
            WorkspaceStaffAccessPlanApplicationErrors.ProfileNotDelegable.Code,
            result.Error.Code);
    }

    [Fact]
    public async Task Inactive_property_and_non_delegable_permission_fail_closed()
    {
        WorkspaceStaffAccessPlanPolicy inactivePropertyPolicy = CreatePolicy(
            Profile(WorkspaceAccessProfileSeeds.FrontDeskKey),
            new StubRoles(owner: true),
            new StubAuthorization(_ => AccessDecision.Allowed()),
            propertiesActive: false);
        var inactiveProperty = await inactivePropertyPolicy.ValidateAsync(
            WorkspaceId,
            WorkspaceStaffOnboardingSource.Invitation,
            WorkspaceAccessProfileSeeds.FrontDeskKey,
            [Guid.NewGuid()],
            Actor.Id,
            CancellationToken.None);

        WorkspaceStaffAccessPlanPolicy forbiddenProfilePolicy = CreatePolicy(
            Profile(WorkspaceAccessProfileSeeds.FrontDeskKey, ["system.root"]),
            new StubRoles(owner: true),
            new StubAuthorization(_ => AccessDecision.Allowed()),
            propertiesActive: true);
        var forbiddenProfile = await forbiddenProfilePolicy.ValidateAsync(
            WorkspaceId,
            WorkspaceStaffOnboardingSource.Invitation,
            WorkspaceAccessProfileSeeds.FrontDeskKey,
            [],
            Actor.Id,
            CancellationToken.None);

        Assert.Equal(
            WorkspaceStaffAccessPlanApplicationErrors.PropertyUnavailable.Code,
            inactiveProperty.Error.Code);
        Assert.Equal(
            WorkspaceStaffAccessPlanApplicationErrors.ProfileNotDelegable.Code,
            forbiddenProfile.Error.Code);
    }

    private static WorkspaceStaffAccessPlanPolicy CreatePolicy(
        AccessProfileDto profile,
        IAccessControlRoleProvisioner roles,
        IAccessAuthorizationService authorization,
        bool propertiesActive) => new(
            new StubProfiles(profile),
            roles,
            authorization,
            new StubProperties(propertiesActive));

    private static AccessProfileDto Profile(
        string key,
        IReadOnlyCollection<string>? permissions = null) => new(
            Guid.NewGuid(),
            WorkspaceAccessScopes.Create(WorkspaceId).Value,
            key,
            key,
            string.Empty,
            AccessProfileStatus.Active,
            1,
            (permissions ?? WorkspaceAccessProfileSeeds.FrontDesk.Permissions).ToArray(),
            0,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

    private sealed class StubProfiles(AccessProfileDto profile) : IAccessProfileProvisioner
    {
        public Task<AccessProfileDto> EnsureProfileAsync(
            AccessScope ownerScope,
            AccessProfileDefinition definition,
            AccessSubject actor,
            CancellationToken cancellationToken = default) => Task.FromResult(profile);

        public Task<AccessProfileDto?> FindProfileByKeyAsync(
            AccessScope ownerScope,
            string key,
            CancellationToken cancellationToken = default) => Task.FromResult<AccessProfileDto?>(profile);

        public Task<AccessProfileAssignmentSet> GetSubjectAssignmentsAsync(
            AccessSubject subject,
            AccessScope ownerScope,
            CancellationToken cancellationToken = default) => Task.FromResult(
                new AccessProfileAssignmentSet(subject, ownerScope, []));

        public Task<AccessProfileAssignmentReconciliation> ReconcileSubjectAssignmentsAsync(
            AccessSubject subject,
            AccessScope ownerScope,
            IReadOnlyCollection<Guid> profileIds,
            AccessSubject actor,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class StubRoles(bool owner) : IAccessControlRoleProvisioner
    {
        public Task<bool> HasAssignmentAsync(
            AccessSubject subject,
            string roleName,
            AccessScope scope,
            CancellationToken cancellationToken = default) => Task.FromResult(
                owner && string.Equals(roleName, WorkspaceAccessRoles.Owner, StringComparison.Ordinal));

        public Task EnsureRoleAsync(AccessControlRoleDefinition role, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task EnsureAssignmentAsync(
            AccessSubject subject,
            string roleName,
            AccessScope scope,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<AccessControlAssignmentRemovalOutcome> RemoveAssignmentAsync(
            AccessSubject subject,
            string roleName,
            AccessScope scope,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<AccessControlPage<AccessControlRoleAssignment>> ListAssignmentsAsync(
            string roleName,
            AccessScope scope,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class StubAuthorization(Func<AccessRequirement, AccessDecision> decide)
        : IAccessAuthorizationService
    {
        public List<AccessRequirement> Requirements { get; } = [];

        public Task<AccessDecision> AuthorizeAsync(
            AccessRequirement requirement,
            CancellationToken cancellationToken)
        {
            this.Requirements.Add(requirement);
            return Task.FromResult(decide(requirement));
        }
    }

    private sealed class StubProperties(bool active) : IWorkspacePropertyProjectionRepository
    {
        public Task<bool> AreAllActiveAsync(
            IReadOnlyCollection<Guid> propertyIds,
            CancellationToken cancellationToken) => Task.FromResult(active);

        public Task ApplyAsync(
            WorkspacePropertyProjectionWriteModel property,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}

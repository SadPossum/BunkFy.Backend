namespace BunkFy.Modules.Workspaces.Tests.Application;

using BunkFy.Modules.Properties.Contracts;
using BunkFy.Modules.Workspaces.Application;
using BunkFy.Modules.Workspaces.Application.Ports;
using BunkFy.Modules.Workspaces.Contracts;
using Gma.Framework.AccessControl;
using Gma.Framework.Results;
using Gma.Framework.Scoping;
using Gma.Modules.AccessControl.Contracts;
using Xunit;

[Trait("Category", "Unit")]
public sealed class WorkspaceMemberAccessManagerTests
{
    private static readonly Guid WorkspaceId = Guid.Parse("36ce099b-205f-4286-af1c-fb19505ead7e");
    private static readonly AccessScope WorkspaceScope = WorkspaceAccessScopes.Create(
        WorkspaceId.ToString("D"));
    private static readonly AccessSubject Actor = AccessSubject.User("owner-a");

    [Fact]
    public async Task Update_denies_before_inspecting_target_membership()
    {
        RecordingRoles roles = new();
        WorkspaceMemberAccessManager manager = CreateManager(
            roles: roles,
            authorization: new RecordingAuthorization(allowed: false));

        Result<WorkspaceMemberAccessDto> result = await manager.UpdateAsync(
            "member-a",
            new WorkspaceMemberAccessUpdate(Guid.NewGuid(), []),
            Actor);

        Assert.True(result.IsFailure);
        Assert.Equal(ScopedAccessProfileManagementErrors.AccessDenied, result.Error);
        Assert.Equal(0, roles.CheckCount);
    }

    [Theory]
    [InlineData(true, true, "Workspaces.AccessOwnerProtected")]
    [InlineData(false, false, "Workspaces.AccessMemberUnavailable")]
    public async Task Update_accepts_only_ordinary_active_members(
        bool owner,
        bool member,
        string expectedErrorCode)
    {
        WorkspaceMemberAccessManager manager = CreateManager(
            roles: new RecordingRoles { IsOwner = owner, IsMember = member });

        Result<WorkspaceMemberAccessDto> result = await manager.UpdateAsync(
            "member-a",
            new WorkspaceMemberAccessUpdate(Guid.NewGuid(), []),
            Actor);

        Assert.True(result.IsFailure);
        Assert.Equal(expectedErrorCode, result.Error.Code);
    }

    [Fact]
    public async Task Update_rejects_archived_or_non_product_profiles_before_reconciliation()
    {
        RecordingScopedManager assignments = new();
        StubProfileManager profiles = new()
        {
            Profile = Profile(
                status: AccessProfileStatus.Archived,
                permissions: [PropertiesAdminPermissionCodes.Read])
        };
        WorkspaceMemberAccessManager manager = CreateManager(
            profiles: profiles,
            assignments: assignments);

        Result<WorkspaceMemberAccessDto> archived = await manager.UpdateAsync(
            "member-a",
            new WorkspaceMemberAccessUpdate(profiles.Profile.Id, []),
            Actor);

        profiles.Profile = Profile(permissions: [AccessControlProfilePermissionCodes.Manage]);
        Result<WorkspaceMemberAccessDto> nonProduct = await manager.UpdateAsync(
            "member-a",
            new WorkspaceMemberAccessUpdate(profiles.Profile.Id, []),
            Actor);

        Assert.Equal(WorkspaceAccessManagementErrors.ProfileUnavailable, archived.Error);
        Assert.Equal(WorkspaceAccessManagementErrors.ProfileUnavailable, nonProduct.Error);
        Assert.Empty(assignments.Reconciliations);
    }

    [Fact]
    public async Task Update_validates_properties_and_reconciles_an_exact_sorted_target_set()
    {
        Guid propertyA = Guid.Parse("c72b6dab-389d-4eec-9322-a60152ff2e52");
        Guid propertyB = Guid.Parse("0c412d48-3a8b-47f1-8935-589618dbe131");
        StubProfileManager profiles = new();
        RecordingScopedManager assignments = new();
        RecordingProperties properties = new();
        WorkspaceMemberAccessManager manager = CreateManager(
            profiles: profiles,
            assignments: assignments,
            properties: properties);

        Result<WorkspaceMemberAccessDto> result = await manager.UpdateAsync(
            "member-a",
            new WorkspaceMemberAccessUpdate(profiles.Profile.Id, [propertyA, propertyB]),
            Actor);

        Assert.True(result.IsSuccess);
        Assert.Equal([propertyB, propertyA], properties.LastPropertyIds);
        ScopedCall call = Assert.Single(assignments.Reconciliations);
        Assert.Equal(Actor, call.Actor);
        Assert.Equal(WorkspaceScope, call.OwnerScope);
        Assert.Equal(
            [propertyB, propertyA],
            call.Targets.Select(target => Guid.Parse(target.AssignmentScope.Segments[1].Value)));
        Assert.Equal([propertyB, propertyA], result.Value.Assignments.Select(item => item.PropertyId!.Value));
    }

    [Fact]
    public async Task Empty_property_selection_creates_one_workspace_wide_assignment()
    {
        StubProfileManager profiles = new();
        RecordingScopedManager assignments = new();
        WorkspaceMemberAccessManager manager = CreateManager(
            profiles: profiles,
            assignments: assignments);

        Result<WorkspaceMemberAccessDto> result = await manager.UpdateAsync(
            "member-a",
            new WorkspaceMemberAccessUpdate(profiles.Profile.Id, []),
            Actor);

        Assert.True(result.IsSuccess);
        AccessProfileAssignmentTarget target = Assert.Single(
            Assert.Single(assignments.Reconciliations).Targets);
        Assert.Equal(WorkspaceScope, target.AssignmentScope);
        Assert.Null(Assert.Single(result.Value.Assignments).PropertyId);
    }

    private static WorkspaceMemberAccessManager CreateManager(
        RecordingRoles? roles = null,
        StubProfileManager? profiles = null,
        RecordingScopedManager? assignments = null,
        RecordingProperties? properties = null,
        RecordingAuthorization? authorization = null) => new(
            roles ?? new RecordingRoles { IsMember = true },
            profiles ?? new StubProfileManager(),
            assignments ?? new RecordingScopedManager(),
            properties ?? new RecordingProperties(),
            authorization ?? new RecordingAuthorization(),
            new StubScopeContext(WorkspaceId.ToString("D")));

    private static AccessProfileDto Profile(
        AccessProfileStatus status = AccessProfileStatus.Active,
        IReadOnlyList<string>? permissions = null) => new(
            Guid.NewGuid(),
            WorkspaceId.ToString("D"),
            "front-desk",
            "Front desk",
            "Daily operations",
            status,
            4,
            permissions ?? [PropertiesAdminPermissionCodes.Read],
            2,
            new DateTimeOffset(2026, 7, 22, 8, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 7, 22, 9, 0, 0, TimeSpan.Zero));

    private sealed class RecordingRoles : IAccessControlRoleProvisioner
    {
        public bool IsOwner { get; init; }
        public bool IsMember { get; init; }
        public int CheckCount { get; private set; }

        public Task<bool> HasAssignmentAsync(
            AccessSubject subject,
            string roleName,
            AccessScope scope,
            CancellationToken cancellationToken = default)
        {
            this.CheckCount++;
            return Task.FromResult(roleName switch
            {
                WorkspaceAccessRoles.Owner => this.IsOwner,
                WorkspaceAccessRoles.MembershipMarker => this.IsMember,
                WorkspaceAccessRoles.LegacyMember => false,
                _ => false
            });
        }

        public Task EnsureRoleAsync(
            AccessControlRoleDefinition role,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

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

    private sealed class StubProfileManager : IAccessProfileManager
    {
        public AccessProfileDto Profile { get; set; } = WorkspaceMemberAccessManagerTests.Profile();

        public Task<Result<AccessProfileDto>> GetProfileAsync(
            Guid profileId,
            AccessScope ownerScope,
            AccessSubject actor,
            CancellationToken cancellationToken = default) => Task.FromResult(Result.Success(this.Profile));

        public Task<Result<AccessControlPage<AccessProfileDto>>> ListProfilesAsync(
            AccessScope ownerScope,
            bool includeArchived,
            int page,
            int pageSize,
            AccessSubject actor,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<Result<IReadOnlyList<string>>> ListAllowedPermissionsAsync(
            AccessScope ownerScope,
            AccessSubject actor,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<Result<AccessProfileDto>> CreateProfileAsync(
            AccessScope ownerScope,
            AccessProfileDefinition definition,
            AccessSubject actor,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<Result<AccessProfileDto>> UpdateProfileAsync(
            Guid profileId,
            AccessScope ownerScope,
            AccessProfileUpdate update,
            AccessSubject actor,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<Result> ArchiveProfileAsync(
            Guid profileId,
            AccessScope ownerScope,
            long expectedVersion,
            AccessSubject actor,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class RecordingScopedManager : IScopedAccessProfileManager
    {
        public List<ScopedCall> Reconciliations { get; } = [];

        public Task<Result<ScopedAccessProfileAssignmentSet>> GetSubjectAssignmentsAsync(
            AccessSubject subject,
            AccessScope ownerScope,
            AccessSubject actor,
            CancellationToken cancellationToken = default) => Task.FromResult(Result.Success(
                new ScopedAccessProfileAssignmentSet(subject, ownerScope, [])));

        public Task<Result<ScopedAccessProfileAssignmentReconciliation>> ReconcileSubjectAssignmentsAsync(
            AccessSubject subject,
            AccessScope ownerScope,
            IReadOnlyCollection<AccessProfileAssignmentTarget> targets,
            AccessSubject actor,
            CancellationToken cancellationToken = default)
        {
            AccessProfileAssignmentTarget[] recorded = targets.ToArray();
            this.Reconciliations.Add(new ScopedCall(subject, ownerScope, recorded, actor));
            return Task.FromResult(Result.Success(new ScopedAccessProfileAssignmentReconciliation(
                subject,
                ownerScope,
                recorded,
                recorded.Length,
                0)));
        }
    }

    private sealed class RecordingProperties : IWorkspacePropertyProjectionRepository
    {
        public IReadOnlyList<Guid> LastPropertyIds { get; private set; } = [];

        public Task<bool> AreAllActiveAsync(
            IReadOnlyCollection<Guid> propertyIds,
            CancellationToken cancellationToken)
        {
            this.LastPropertyIds = propertyIds.ToArray();
            return Task.FromResult(true);
        }

        public Task ApplyAsync(
            WorkspacePropertyProjectionWriteModel property,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class RecordingAuthorization(bool allowed = true) : IAccessAuthorizationService
    {
        public Task<AccessDecision> AuthorizeAsync(
            AccessRequirement requirement,
            CancellationToken cancellationToken) => Task.FromResult(allowed
                ? AccessDecision.Allowed()
                : AccessDecision.Denied("test.denied"));
    }

    private sealed class StubScopeContext(string scopeId) : IScopeContext
    {
        public bool IsEnabled => true;
        public string? ScopeId => scopeId;
    }

    private sealed record ScopedCall(
        AccessSubject Subject,
        AccessScope OwnerScope,
        IReadOnlyList<AccessProfileAssignmentTarget> Targets,
        AccessSubject Actor);
}

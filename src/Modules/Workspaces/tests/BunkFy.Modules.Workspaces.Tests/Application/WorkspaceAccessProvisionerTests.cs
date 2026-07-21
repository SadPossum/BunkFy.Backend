namespace BunkFy.Modules.Workspaces.Tests;

using BunkFy.Modules.Workspaces.Application;
using BunkFy.Modules.Workspaces.Contracts;
using Gma.Framework.AccessControl;
using Gma.Modules.AccessControl.Contracts;
using Xunit;

[Trait("Category", "Unit")]
public sealed class WorkspaceAccessProvisionerTests
{
    private const string WorkspaceId = "workspace-a";
    private static readonly AccessScope WorkspaceScope = WorkspaceAccessScopes.Create(WorkspaceId);

    [Fact]
    public async Task Default_member_provisioning_preserves_custom_profiles_and_retires_legacy_access_last()
    {
        List<string> operations = [];
        FakeRoles roles = new(operations);
        FakeProfiles profiles = new(operations);
        AccessSubject member = AccessSubject.User("member-a");
        roles.Add(member, WorkspaceAccessRoles.LegacyMember, WorkspaceScope);
        AccessProfileDto custom = profiles.AddProfile(WorkspaceScope, "custom", ["reservations.read"]);
        profiles.Assign(member, WorkspaceScope, custom.Id);
        WorkspaceAccessProvisioner provisioner = new(roles, profiles);

        await provisioner.ProvisionDefaultMemberAsync(WorkspaceId, member.Id, CancellationToken.None);
        await provisioner.ProvisionDefaultMemberAsync(WorkspaceId, member.Id, CancellationToken.None);

        Assert.True(roles.Has(member, WorkspaceAccessRoles.MembershipMarker, WorkspaceScope));
        Assert.False(roles.Has(member, WorkspaceAccessRoles.LegacyMember, WorkspaceScope));
        Assert.Empty(roles.Definitions[WorkspaceAccessRoles.MembershipMarker]);
        AccessProfileDto frontDesk = Assert.Single(
            profiles.Profiles,
            profile => profile.Key == WorkspaceAccessProfileSeeds.FrontDeskKey);
        Assert.Equal(
            new[] { custom.Id, frontDesk.Id }.Order().ToArray(),
            profiles.AssignedProfileIds(member, WorkspaceScope).Order().ToArray());

        int markerAssigned = operations.IndexOf("role:assign:bunkfy-workspace-member-v2:member-a");
        int reconciled = operations.IndexOf("profile:reconcile:member-a");
        int legacyRemoved = operations.IndexOf("role:remove:bunkfy-workspace-member:member-a");
        Assert.True(markerAssigned >= 0 && markerAssigned < reconciled);
        Assert.True(reconciled < legacyRemoved);
    }

    [Fact]
    public async Task Backfill_drains_every_legacy_page_once_and_is_replay_safe()
    {
        List<string> operations = [];
        FakeRoles roles = new(operations);
        FakeProfiles profiles = new(operations);
        WorkspaceAccessProvisioner provisioner = new(roles, profiles);
        AccessSubject[] members = Enumerable.Range(1, 205)
            .Select(index => AccessSubject.User($"member-{index:D3}"))
            .ToArray();
        foreach (AccessSubject member in members)
        {
            roles.Add(member, WorkspaceAccessRoles.LegacyMember, WorkspaceScope);
        }

        AccessProfileDto custom = profiles.AddProfile(WorkspaceScope, "custom", ["inventory.read"]);
        profiles.Assign(members[0], WorkspaceScope, custom.Id);

        WorkspaceAccessBootstrapResult first = await provisioner.BackfillLegacyMembersAsync(
            WorkspaceId,
            CancellationToken.None);
        WorkspaceAccessBootstrapResult replay = await provisioner.BackfillLegacyMembersAsync(
            WorkspaceId,
            CancellationToken.None);

        Assert.Equal(WorkspaceAccessProfileSeeds.Version, first.SeedVersion);
        Assert.Equal(WorkspaceAccessProfileSeeds.All.Count, first.SeedProfileCount);
        Assert.Equal(205, first.MigratedMemberCount);
        Assert.Equal(0, replay.MigratedMemberCount);
        Assert.Equal(WorkspaceAccessProfileSeeds.All.Count, profiles.Profiles.Length - 1);
        Assert.All(roles.ListRequests, request => Assert.Equal(1, request.Page));
        Assert.Equal(5, roles.ListRequests.Count);

        AccessProfileDto frontDesk = Assert.Single(
            profiles.Profiles,
            profile => profile.Key == WorkspaceAccessProfileSeeds.FrontDeskKey);
        foreach (AccessSubject member in members)
        {
            Assert.True(roles.Has(member, WorkspaceAccessRoles.MembershipMarker, WorkspaceScope));
            Assert.False(roles.Has(member, WorkspaceAccessRoles.LegacyMember, WorkspaceScope));
            Assert.Contains(frontDesk.Id, profiles.AssignedProfileIds(member, WorkspaceScope));
        }

        Assert.Contains(custom.Id, profiles.AssignedProfileIds(members[0], WorkspaceScope));
    }

    [Fact]
    public async Task Failed_profile_reconciliation_keeps_the_legacy_grant_for_a_safe_retry()
    {
        List<string> operations = [];
        FakeRoles roles = new(operations);
        FakeProfiles profiles = new(operations) { FailReconciliation = true };
        AccessSubject member = AccessSubject.User("member-a");
        roles.Add(member, WorkspaceAccessRoles.LegacyMember, WorkspaceScope);
        WorkspaceAccessProvisioner provisioner = new(roles, profiles);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provisioner.ProvisionDefaultMemberAsync(WorkspaceId, member.Id, CancellationToken.None));

        Assert.True(roles.Has(member, WorkspaceAccessRoles.MembershipMarker, WorkspaceScope));
        Assert.True(roles.Has(member, WorkspaceAccessRoles.LegacyMember, WorkspaceScope));
        Assert.DoesNotContain("role:remove:bunkfy-workspace-member:member-a", operations);

        profiles.FailReconciliation = false;
        await provisioner.ProvisionDefaultMemberAsync(WorkspaceId, member.Id, CancellationToken.None);

        Assert.False(roles.Has(member, WorkspaceAccessRoles.LegacyMember, WorkspaceScope));
    }

    [Fact]
    public async Task Backfill_rejects_an_unexpected_non_user_legacy_assignment()
    {
        FakeRoles roles = new([]);
        roles.Add(AccessSubject.Service("legacy-service"), WorkspaceAccessRoles.LegacyMember, WorkspaceScope);
        WorkspaceAccessProvisioner provisioner = new(roles, new FakeProfiles([]));

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provisioner.BackfillLegacyMembersAsync(WorkspaceId, CancellationToken.None));

        Assert.Contains("non-user", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Inspection_counts_seed_states_and_every_assignment_page()
    {
        FakeRoles roles = new([]);
        FakeProfiles profiles = new([]);
        profiles.AddProfile(
            WorkspaceScope,
            WorkspaceAccessProfileSeeds.ManagerKey,
            WorkspaceAccessProfileSeeds.Manager.Permissions);
        profiles.AddProfile(
            WorkspaceScope,
            WorkspaceAccessProfileSeeds.FrontDeskKey,
            WorkspaceAccessProfileSeeds.FrontDesk.Permissions,
            AccessProfileStatus.Archived);
        profiles.AddProfile(
            WorkspaceScope,
            WorkspaceAccessProfileSeeds.HousekeepingKey,
            WorkspaceAccessProfileSeeds.Housekeeping.Permissions);

        foreach (int index in Enumerable.Range(1, 205))
        {
            roles.Add(
                AccessSubject.User($"legacy-{index:D3}"),
                WorkspaceAccessRoles.LegacyMember,
                WorkspaceScope);
        }

        foreach (int index in Enumerable.Range(1, 101))
        {
            roles.Add(
                AccessSubject.User($"marker-{index:D3}"),
                WorkspaceAccessRoles.MembershipMarker,
                WorkspaceScope);
        }

        WorkspaceAccessBootstrapStatus status = await new WorkspaceAccessProvisioner(roles, profiles)
            .InspectAsync(WorkspaceId, CancellationToken.None);

        Assert.Equal(WorkspaceAccessProfileSeeds.Version, status.SeedVersion);
        Assert.Equal(WorkspaceAccessProfileSeeds.All.Count, status.ExpectedSeedProfileCount);
        Assert.Equal(2, status.ActiveSeedProfileCount);
        Assert.Equal(1, status.ArchivedSeedProfileCount);
        Assert.Equal(205, status.LegacyMemberCount);
        Assert.Equal(101, status.MarkerMemberCount);
        Assert.True(status.RequiresBackfill);
        Assert.Equal(
            [
                (WorkspaceAccessRoles.LegacyMember, 1, 100),
                (WorkspaceAccessRoles.LegacyMember, 2, 100),
                (WorkspaceAccessRoles.LegacyMember, 3, 100),
                (WorkspaceAccessRoles.MembershipMarker, 1, 100),
                (WorkspaceAccessRoles.MembershipMarker, 2, 100)
            ],
            roles.ListRequests);
    }

    [Fact]
    public async Task Active_workspace_initialization_ensures_every_seed_without_overwriting_existing_profiles()
    {
        FakeProfiles profiles = new([]);
        AccessProfileDto existing = profiles.AddProfile(
            WorkspaceScope,
            WorkspaceAccessProfileSeeds.ManagerKey,
            ["properties.read"]);
        WorkspaceAccessProvisioner provisioner = new(new FakeRoles([]), profiles);

        await provisioner.EnsureSeedProfilesAsync(WorkspaceId, CancellationToken.None);
        await provisioner.EnsureSeedProfilesAsync(WorkspaceId, CancellationToken.None);

        Assert.Equal(WorkspaceAccessProfileSeeds.All.Count, profiles.Profiles.Length);
        AccessProfileDto manager = Assert.Single(
            profiles.Profiles,
            profile => profile.Key == WorkspaceAccessProfileSeeds.ManagerKey);
        Assert.Equal(existing.Id, manager.Id);
        Assert.Equal(["properties.read"], manager.Permissions);
    }

    [Fact]
    public async Task Backfill_stops_before_member_migration_when_a_seed_is_archived()
    {
        FakeRoles roles = new([]);
        FakeProfiles profiles = new([]);
        profiles.AddProfile(
            WorkspaceScope,
            WorkspaceAccessProfileSeeds.ManagerKey,
            WorkspaceAccessProfileSeeds.Manager.Permissions,
            AccessProfileStatus.Archived);
        AccessSubject member = AccessSubject.User("member-a");
        roles.Add(member, WorkspaceAccessRoles.LegacyMember, WorkspaceScope);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new WorkspaceAccessProvisioner(roles, profiles)
                .BackfillLegacyMembersAsync(WorkspaceId, CancellationToken.None));

        Assert.True(roles.Has(member, WorkspaceAccessRoles.LegacyMember, WorkspaceScope));
        Assert.False(roles.Has(member, WorkspaceAccessRoles.MembershipMarker, WorkspaceScope));
        Assert.Empty(profiles.AssignedProfileIds(member, WorkspaceScope));
    }

    [Fact]
    public async Task Lifecycle_snapshot_migrates_only_legacy_access_and_preserves_custom_profiles()
    {
        FakeRoles roles = new([]);
        FakeProfiles profiles = new([]);
        AccessSubject member = AccessSubject.User("member-a");
        roles.Add(member, WorkspaceAccessRoles.LegacyMember, WorkspaceScope);
        AccessProfileDto custom = profiles.AddProfile(
            WorkspaceScope,
            "night-auditor",
            ["reservations.read"]);
        profiles.Assign(member, WorkspaceScope, custom.Id);

        IReadOnlyCollection<Guid> snapshot = await new WorkspaceAccessProvisioner(roles, profiles)
            .CaptureRestorableProfileIdsAsync(WorkspaceId, member.Id, CancellationToken.None);

        AccessProfileDto frontDesk = Assert.Single(
            profiles.Profiles,
            profile => profile.Key == WorkspaceAccessProfileSeeds.FrontDeskKey);
        Assert.Equal(new[] { custom.Id, frontDesk.Id }.Order(), snapshot.Order());
        Assert.True(roles.Has(member, WorkspaceAccessRoles.MembershipMarker, WorkspaceScope));
        Assert.False(roles.Has(member, WorkspaceAccessRoles.LegacyMember, WorkspaceScope));
    }

    [Fact]
    public async Task Lifecycle_denial_removes_profiles_marker_and_legacy_assignment_idempotently()
    {
        FakeRoles roles = new([]);
        FakeProfiles profiles = new([]);
        AccessSubject member = AccessSubject.User("member-a");
        roles.Add(member, WorkspaceAccessRoles.MembershipMarker, WorkspaceScope);
        roles.Add(member, WorkspaceAccessRoles.LegacyMember, WorkspaceScope);
        AccessProfileDto profile = profiles.AddProfile(
            WorkspaceScope,
            "custom",
            ["inventory.read"]);
        profiles.Assign(member, WorkspaceScope, profile.Id);
        WorkspaceAccessProvisioner provisioner = new(roles, profiles);

        await provisioner.DenyMemberAsync(WorkspaceId, member.Id, CancellationToken.None);
        await provisioner.DenyMemberAsync(WorkspaceId, member.Id, CancellationToken.None);

        Assert.Empty(profiles.AssignedProfileIds(member, WorkspaceScope));
        Assert.False(roles.Has(member, WorkspaceAccessRoles.MembershipMarker, WorkspaceScope));
        Assert.False(roles.Has(member, WorkspaceAccessRoles.LegacyMember, WorkspaceScope));
    }

    [Fact]
    public async Task Lifecycle_restoration_installs_only_the_exact_snapshot_and_marker()
    {
        FakeRoles roles = new([]);
        FakeProfiles profiles = new([]);
        AccessSubject member = AccessSubject.User("member-a");
        AccessProfileDto first = profiles.AddProfile(
            WorkspaceScope,
            "first",
            ["properties.read"]);
        AccessProfileDto second = profiles.AddProfile(
            WorkspaceScope,
            "second",
            ["reservations.read"]);
        profiles.Assign(member, WorkspaceScope, second.Id);

        await new WorkspaceAccessProvisioner(roles, profiles).RestoreMemberAsync(
            WorkspaceId,
            member.Id,
            [first.Id],
            CancellationToken.None);

        Assert.Equal([first.Id], profiles.AssignedProfileIds(member, WorkspaceScope));
        Assert.True(roles.Has(member, WorkspaceAccessRoles.MembershipMarker, WorkspaceScope));
        Assert.False(roles.Has(member, WorkspaceAccessRoles.LegacyMember, WorkspaceScope));
    }

    private sealed class FakeRoles(List<string> operations) : IAccessControlRoleProvisioner
    {
        private readonly HashSet<RoleAssignment> assignments = [];

        public Dictionary<string, IReadOnlyList<string>> Definitions { get; } = new(StringComparer.Ordinal);
        public List<(string Role, int Page, int PageSize)> ListRequests { get; } = [];

        public void Add(AccessSubject subject, string role, AccessScope scope) =>
            this.assignments.Add(new RoleAssignment(subject.Kind, subject.Id, role, scope.Value));

        public bool Has(AccessSubject subject, string role, AccessScope scope) =>
            this.assignments.Contains(new RoleAssignment(subject.Kind, subject.Id, role, scope.Value));

        public Task EnsureRoleAsync(
            AccessControlRoleDefinition role,
            CancellationToken cancellationToken = default)
        {
            this.Definitions[role.Name] = role.Permissions.ToArray();
            operations.Add($"role:ensure:{role.Name}");
            return Task.CompletedTask;
        }

        public Task EnsureAssignmentAsync(
            AccessSubject subject,
            string roleName,
            AccessScope scope,
            CancellationToken cancellationToken = default)
        {
            this.Add(subject, roleName, scope);
            operations.Add($"role:assign:{roleName}:{subject.Id}");
            return Task.CompletedTask;
        }

        public Task<AccessControlAssignmentRemovalOutcome> RemoveAssignmentAsync(
            AccessSubject subject,
            string roleName,
            AccessScope scope,
            CancellationToken cancellationToken = default)
        {
            bool removed = this.assignments.Remove(new RoleAssignment(
                subject.Kind,
                subject.Id,
                roleName,
                scope.Value));
            operations.Add($"role:remove:{roleName}:{subject.Id}");
            return Task.FromResult(removed
                ? AccessControlAssignmentRemovalOutcome.Removed
                : AccessControlAssignmentRemovalOutcome.NotFound);
        }

        public Task<bool> HasAssignmentAsync(
            AccessSubject subject,
            string roleName,
            AccessScope scope,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(this.Has(subject, roleName, scope));

        public Task<AccessControlPage<AccessControlRoleAssignment>> ListAssignmentsAsync(
            string roleName,
            AccessScope scope,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            this.ListRequests.Add((roleName, page, pageSize));
            RoleAssignment[] matching = this.assignments
                .Where(assignment =>
                    assignment.Role == roleName &&
                    assignment.Scope == scope.Value)
                .OrderBy(assignment => assignment.SubjectId, StringComparer.Ordinal)
                .ToArray();
            AccessControlRoleAssignment[] items = matching
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(assignment => new AccessControlRoleAssignment(
                    Guid.NewGuid(),
                    assignment.SubjectKind,
                    assignment.SubjectId,
                    assignment.Role,
                    scope,
                    DateTimeOffset.UnixEpoch))
                .ToArray();
            return Task.FromResult(new AccessControlPage<AccessControlRoleAssignment>(
                items,
                page,
                pageSize,
                matching.Length > page * pageSize));
        }

        private sealed record RoleAssignment(
            AccessSubjectKind SubjectKind,
            string SubjectId,
            string Role,
            string Scope);
    }

    private sealed class FakeProfiles(List<string> operations) : IAccessProfileProvisioner
    {
        private readonly Dictionary<(AccessSubjectKind Kind, string SubjectId, string Scope), HashSet<Guid>> assignments = [];
        private readonly Dictionary<(string Scope, string Key), AccessProfileDto> profiles = [];

        public bool FailReconciliation { get; set; }
        public AccessProfileDto[] Profiles => this.profiles.Values.ToArray();

        public AccessProfileDto AddProfile(
            AccessScope scope,
            string key,
            IReadOnlyCollection<string> permissions,
            AccessProfileStatus status = AccessProfileStatus.Active)
        {
            AccessProfileDto profile = CreateProfile(scope, key, key, permissions, status);
            this.profiles[(scope.Value, key)] = profile;
            return profile;
        }

        public void Assign(AccessSubject subject, AccessScope scope, Guid profileId) =>
            this.GetAssignmentSet(subject, scope).Add(profileId);

        public Guid[] AssignedProfileIds(AccessSubject subject, AccessScope scope) =>
            this.GetAssignmentSet(subject, scope).ToArray();

        public Task<AccessProfileDto> EnsureProfileAsync(
            AccessScope ownerScope,
            AccessProfileDefinition definition,
            AccessSubject actor,
            CancellationToken cancellationToken = default)
        {
            if (!this.profiles.TryGetValue((ownerScope.Value, definition.Key), out AccessProfileDto? profile))
            {
                profile = CreateProfile(
                    ownerScope,
                    definition.Key,
                    definition.DisplayName,
                    definition.Permissions);
                this.profiles[(ownerScope.Value, definition.Key)] = profile;
            }

            operations.Add($"profile:ensure:{definition.Key}");
            return Task.FromResult(profile);
        }

        public Task<AccessProfileDto?> FindProfileByKeyAsync(
            AccessScope ownerScope,
            string key,
            CancellationToken cancellationToken = default)
        {
            this.profiles.TryGetValue((ownerScope.Value, key), out AccessProfileDto? profile);
            return Task.FromResult(profile);
        }

        public Task<AccessProfileAssignmentSet> GetSubjectAssignmentsAsync(
            AccessSubject subject,
            AccessScope ownerScope,
            CancellationToken cancellationToken = default)
        {
            HashSet<Guid> assigned = this.GetAssignmentSet(subject, ownerScope);
            AccessProfileDto[] current = this.profiles.Values
                .Where(profile => assigned.Contains(profile.Id))
                .ToArray();
            return Task.FromResult(new AccessProfileAssignmentSet(subject, ownerScope, current));
        }

        public Task<AccessProfileAssignmentReconciliation> ReconcileSubjectAssignmentsAsync(
            AccessSubject subject,
            AccessScope ownerScope,
            IReadOnlyCollection<Guid> profileIds,
            AccessSubject actor,
            CancellationToken cancellationToken = default)
        {
            operations.Add($"profile:reconcile:{subject.Id}");
            if (this.FailReconciliation)
            {
                throw new InvalidOperationException("Profile reconciliation failed.");
            }

            HashSet<Guid> assigned = this.GetAssignmentSet(subject, ownerScope);
            int assignedCount = profileIds.Count(profileId => !assigned.Contains(profileId));
            int unassignedCount = assigned.Count(profileId => !profileIds.Contains(profileId));
            assigned.Clear();
            assigned.UnionWith(profileIds);
            return Task.FromResult(new AccessProfileAssignmentReconciliation(
                subject,
                ownerScope,
                profileIds.ToArray(),
                assignedCount,
                unassignedCount));
        }

        private HashSet<Guid> GetAssignmentSet(AccessSubject subject, AccessScope scope)
        {
            (AccessSubjectKind Kind, string Id, string Scope) key = (subject.Kind, subject.Id, scope.Value);
            if (!this.assignments.TryGetValue(key, out HashSet<Guid>? assigned))
            {
                assigned = [];
                this.assignments[key] = assigned;
            }

            return assigned;
        }

        private static AccessProfileDto CreateProfile(
            AccessScope scope,
            string key,
            string displayName,
            IReadOnlyCollection<string> permissions,
            AccessProfileStatus status = AccessProfileStatus.Active) => new(
            Guid.NewGuid(),
            scope.Value,
            key,
            displayName,
            string.Empty,
            status,
            1,
            permissions.ToArray(),
            0,
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch);
    }
}

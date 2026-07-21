namespace BunkFy.Modules.Workspaces.Tests;

using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Workspaces.Application;
using BunkFy.Modules.Workspaces.Application.Commands;
using BunkFy.Modules.Workspaces.Application.Handlers;
using BunkFy.Modules.Workspaces.Application.Ports;
using BunkFy.Modules.Workspaces.Contracts;
using BunkFy.Modules.Workspaces.Domain;
using Gma.Framework.AccessControl;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Pagination;
using Gma.Modules.AccessControl.Contracts;
using Gma.Modules.Organizations.Contracts;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

[Trait("Category", "Unit")]
public sealed class WorkspaceStaffAccessFlowTests
{
    private static readonly Guid OrganizationId = Guid.NewGuid();
    private static readonly string ScopeId = OrganizationId.ToString("D");
    private static readonly DateTimeOffset Now = new(2026, 7, 21, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Suspension_preparation_persists_the_exact_profile_snapshot()
    {
        List<string> operations = [];
        FakeRoles roles = new(operations);
        FakeProfiles profiles = new(operations);
        AccessSubject subject = AccessSubject.User("member-a");
        AccessScope scope = WorkspaceAccessScopes.Create(ScopeId);
        AccessProfileDto custom = profiles.AddProfile(scope, "night-auditor");
        profiles.Assign(subject, scope, custom.Id);
        FakeProcessRepository repository = new();
        PrepareWorkspaceStaffAccessCommandHandler handler = new(
            repository,
            new WorkspaceAccessProvisioner(roles, profiles),
            new TestClock());
        StaffLifecyclePolicyContext context = CreateContext(
            StaffLifecycleTransition.Suspend,
            StaffStatus.Active,
            StaffStatus.Suspended,
            expectedVersion: 1);

        Result<WorkspaceStaffAccessPreparation> result = await handler.HandleAsync(
            new PrepareWorkspaceStaffAccessCommand(context),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error.Code);
        Assert.True(result.Value.RequiresAccessDenial);
        WorkspaceStaffAccessProcess process = Assert.Single(repository.Processes);
        Assert.Equal(WorkspaceStaffAccessProcessState.Prepared, process.State);
        Assert.Equal([custom.Id], process.ProfileSnapshots.Select(snapshot => snapshot.ProfileId));
    }

    [Fact]
    public async Task Denial_changes_membership_before_removing_product_access()
    {
        List<string> operations = [];
        FakeRoles roles = new(operations);
        FakeProfiles profiles = new(operations);
        FakeMembershipLifecycle memberships = new(operations);
        AccessSubject subject = AccessSubject.User("member-a");
        AccessScope scope = WorkspaceAccessScopes.Create(ScopeId);
        AccessProfileDto custom = profiles.AddProfile(scope, "night-auditor");
        profiles.Assign(subject, scope, custom.Id);
        roles.Add(subject, WorkspaceAccessRoles.MembershipMarker, scope);
        WorkspaceStaffAccessProcess process = CreateProcess(
            WorkspaceStaffAccessTargetState.Suspended,
            targetVersion: 2,
            [custom.Id]);
        FakeProcessRepository repository = new(process);
        WorkspaceStaffAccessDenier denier = new(
            memberships,
            new WorkspaceAccessProvisioner(roles, profiles),
            new TestClock(),
            NullLogger<WorkspaceStaffAccessDenier>.Instance);
        DenyWorkspaceStaffAccessCommandHandler handler = new(repository, denier);

        Result<WorkspaceStaffAccessCoordinationOutcome> result = await handler.HandleAsync(
            new DenyWorkspaceStaffAccessCommand(process.Id),
            CancellationToken.None);

        Assert.Equal(WorkspaceStaffAccessCoordinationOutcome.Allowed, result.Value);
        Assert.Equal(WorkspaceStaffAccessProcessState.AwaitingStaffCommit, process.State);
        Assert.Empty(profiles.AssignedProfileIds(subject, scope));
        Assert.False(roles.Has(subject, WorkspaceAccessRoles.MembershipMarker, scope));
        Assert.True(operations.IndexOf("membership:Suspended") < operations.IndexOf("profiles:reconcile"));
    }

    [Fact]
    public async Task Owner_protection_keeps_profiles_and_blocks_the_staff_transition()
    {
        List<string> operations = [];
        FakeRoles roles = new(operations);
        FakeProfiles profiles = new(operations);
        FakeMembershipLifecycle memberships = new(
            operations,
            OrganizationMembershipLifecycleOutcome.OwnerProtected);
        AccessSubject subject = AccessSubject.User("member-a");
        AccessScope scope = WorkspaceAccessScopes.Create(ScopeId);
        AccessProfileDto custom = profiles.AddProfile(scope, "manager");
        profiles.Assign(subject, scope, custom.Id);
        WorkspaceStaffAccessProcess process = CreateProcess(
            WorkspaceStaffAccessTargetState.Suspended,
            targetVersion: 2,
            [custom.Id]);
        WorkspaceStaffAccessDenier denier = new(
            memberships,
            new WorkspaceAccessProvisioner(roles, profiles),
            new TestClock(),
            NullLogger<WorkspaceStaffAccessDenier>.Instance);
        DenyWorkspaceStaffAccessCommandHandler handler = new(
            new FakeProcessRepository(process),
            denier);

        Result<WorkspaceStaffAccessCoordinationOutcome> result = await handler.HandleAsync(
            new DenyWorkspaceStaffAccessCommand(process.Id),
            CancellationToken.None);

        Assert.Equal(WorkspaceStaffAccessCoordinationOutcome.OwnerProtected, result.Value);
        Assert.Equal(WorkspaceStaffAccessProcessState.Prepared, process.State);
        Assert.Equal([custom.Id], profiles.AssignedProfileIds(subject, scope));
        Assert.Equal("Workspaces.StaffAccessOwnerProtected", process.FailureCode);
    }

    [Fact]
    public async Task Resume_copies_the_latest_completed_suspension_snapshot_and_stays_denied()
    {
        Guid first = Guid.NewGuid();
        Guid second = Guid.NewGuid();
        WorkspaceStaffAccessProcess suspension = CreateProcess(
            WorkspaceStaffAccessTargetState.Suspended,
            targetVersion: 2,
            [first, second]);
        Assert.True(suspension.MarkAwaitingStaffCommit(Now).IsSuccess);
        Assert.True(suspension.ObserveStaffCommit(Now).IsSuccess);
        FakeProcessRepository repository = new(suspension);
        PrepareWorkspaceStaffAccessCommandHandler handler = new(
            repository,
            new WorkspaceAccessProvisioner(new FakeRoles([]), new FakeProfiles([])),
            new TestClock());
        StaffLifecyclePolicyContext context = CreateContext(
            StaffLifecycleTransition.Resume,
            StaffStatus.Suspended,
            StaffStatus.Active,
            expectedVersion: 2);

        Result<WorkspaceStaffAccessPreparation> result = await handler.HandleAsync(
            new PrepareWorkspaceStaffAccessCommand(context),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error.Code);
        Assert.False(result.Value.RequiresAccessDenial);
        WorkspaceStaffAccessProcess resume = repository.Processes.Single(process => process.Id != suspension.Id);
        Assert.Equal(WorkspaceStaffAccessProcessState.AwaitingStaffCommit, resume.State);
        Assert.Equal(
            new[] { first, second }.Order(),
            resume.ProfileSnapshots.Select(snapshot => snapshot.ProfileId).Order());
    }

    [Fact]
    public async Task Immediate_resume_uses_the_observed_suspension_without_a_stale_persistence_read()
    {
        Guid profileId = Guid.NewGuid();
        WorkspaceStaffAccessProcess suspension = CreateProcess(
            WorkspaceStaffAccessTargetState.Suspended,
            targetVersion: 2,
            [profileId]);
        Assert.True(suspension.MarkAwaitingStaffCommit(Now).IsSuccess);
        FakeProcessRepository repository = new(suspension)
        {
            FailLatestSuspensionRead = true
        };
        PrepareWorkspaceStaffAccessCommandHandler handler = new(
            repository,
            new WorkspaceAccessProvisioner(new FakeRoles([]), new FakeProfiles([])),
            new TestClock());
        StaffLifecyclePolicyContext context = CreateContext(
            StaffLifecycleTransition.Resume,
            StaffStatus.Suspended,
            StaffStatus.Active,
            expectedVersion: 2);

        Result<WorkspaceStaffAccessPreparation> result = await handler.HandleAsync(
            new PrepareWorkspaceStaffAccessCommand(context),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error.Code);
        Assert.Equal(WorkspaceStaffAccessProcessState.Completed, suspension.State);
        WorkspaceStaffAccessProcess resume = repository.Processes.Single(process => process.Id != suspension.Id);
        Assert.Equal([profileId], resume.ProfileSnapshots.Select(snapshot => snapshot.ProfileId));
    }

    [Fact]
    public async Task Active_staff_event_restores_membership_marker_and_exact_profiles()
    {
        List<string> operations = [];
        FakeRoles roles = new(operations);
        FakeProfiles profiles = new(operations);
        FakeMembershipLifecycle memberships = new(operations);
        AccessScope scope = WorkspaceAccessScopes.Create(ScopeId);
        AccessSubject subject = AccessSubject.User("member-a");
        AccessProfileDto first = profiles.AddProfile(scope, "front-desk");
        profiles.AddProfile(scope, "not-restored");
        WorkspaceStaffAccessProcess process = CreateProcess(
            WorkspaceStaffAccessTargetState.Active,
            targetVersion: 3,
            [first.Id]);
        Assert.True(process.MarkAwaitingStaffCommit(Now).IsSuccess);
        FakeProcessRepository repository = new(process);
        WorkspaceAccessProvisioner access = new(roles, profiles);
        WorkspaceStaffAccessRestorer restorer = new(
            memberships,
            access,
            new TestClock(),
            NullLogger<WorkspaceStaffAccessRestorer>.Instance);
        StaffLifecycleWorkspaceAccessHandler handler = new(
            repository,
            restorer,
            new TestClock());

        await handler.HandleAsync(new StaffMemberLifecycleChangedIntegrationEvent(
            Guid.NewGuid(),
            ScopeId,
            Now,
            process.StaffMemberId,
            StaffStatus.Active,
            new DateOnly(2026, 7, 21),
            3,
            "user:owner"), CancellationToken.None);

        Assert.Equal(WorkspaceStaffAccessProcessState.Completed, process.State);
        Assert.True(roles.Has(subject, WorkspaceAccessRoles.MembershipMarker, scope));
        Assert.Equal([first.Id], profiles.AssignedProfileIds(subject, scope));
        Assert.True(operations.IndexOf("membership:Active") < operations.IndexOf("profiles:reconcile"));
    }

    [Fact]
    public async Task Retry_completes_a_pending_restoration_with_the_exact_snapshot()
    {
        List<string> operations = [];
        FakeRoles roles = new(operations);
        FakeProfiles profiles = new(operations);
        FakeMembershipLifecycle memberships = new(operations);
        AccessScope scope = WorkspaceAccessScopes.Create(ScopeId);
        AccessSubject subject = AccessSubject.User("member-a");
        AccessProfileDto restored = profiles.AddProfile(scope, "front-desk");
        profiles.AddProfile(scope, "not-restored");
        WorkspaceStaffAccessProcess process = CreateProcess(
            WorkspaceStaffAccessTargetState.Active,
            targetVersion: 3,
            [restored.Id]);
        Assert.True(process.MarkAwaitingStaffCommit(Now).IsSuccess);
        Assert.True(process.ObserveStaffCommit(Now).IsSuccess);
        Assert.True(process.RecordFailure("Workspaces.StaffAccessRestoreFailed", Now).IsSuccess);
        FakeProcessRepository repository = new(process);
        WorkspaceAccessProvisioner access = new(roles, profiles);
        WorkspaceStaffAccessDenier denier = new(
            memberships,
            access,
            new TestClock(),
            NullLogger<WorkspaceStaffAccessDenier>.Instance);
        WorkspaceStaffAccessRestorer restorer = new(
            memberships,
            access,
            new TestClock(),
            NullLogger<WorkspaceStaffAccessRestorer>.Instance);
        RetryWorkspaceStaffAccessProcessCommandHandler handler = new(repository, denier, restorer);

        Result<WorkspaceStaffAccessProcessDto> result = await handler.HandleAsync(
            new RetryWorkspaceStaffAccessProcessCommand(process.Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error.Code);
        Assert.Equal(WorkspaceStaffAccessProcessStatus.Completed, result.Value.Status);
        Assert.Null(result.Value.FailureCode);
        Assert.True(roles.Has(subject, WorkspaceAccessRoles.MembershipMarker, scope));
        Assert.Equal([restored.Id], profiles.AssignedProfileIds(subject, scope));
    }

    [Fact]
    public async Task Product_policy_denies_direct_organization_membership_changes()
    {
        WorkspaceOrganizationMembershipChangePolicy policy = new();

        OrganizationMembershipChangePolicyDecision decision = await policy.EvaluateAsync(
            new OrganizationMembershipChangePolicyRequest(
                OrganizationId,
                "owner",
                "member-a",
                OrganizationMembershipRole.Member,
                OrganizationMembershipStatus.Active,
                OrganizationMembershipStatus.Suspended),
            CancellationToken.None);

        Assert.Equal(OrganizationMembershipChangePolicyDecision.Denied, decision);
    }

    private static StaffLifecyclePolicyContext CreateContext(
        StaffLifecycleTransition transition,
        StaffStatus previousStatus,
        StaffStatus targetStatus,
        long expectedVersion) => new(
        Guid.NewGuid(),
        ScopeId,
        StaffId,
        "member-a",
        transition,
        previousStatus,
        targetStatus,
        new DateOnly(2026, 7, 21),
        expectedVersion,
        expectedVersion + 1,
        "user:owner");

    private static readonly Guid StaffId = Guid.NewGuid();

    private static WorkspaceStaffAccessProcess CreateProcess(
        WorkspaceStaffAccessTargetState targetState,
        long targetVersion,
        IReadOnlyCollection<Guid> profiles) => WorkspaceStaffAccessProcess.Create(
        Guid.NewGuid(),
        ScopeId,
        StaffId,
        "member-a",
        targetState,
        targetVersion,
        new DateOnly(2026, 7, 21),
        "user:owner",
        profiles,
        Now).Value;

    private sealed class FakeProcessRepository(params WorkspaceStaffAccessProcess[] processes)
        : IWorkspaceStaffAccessProcessRepository
    {
        public bool FailLatestSuspensionRead { get; init; }
        public List<WorkspaceStaffAccessProcess> Processes { get; } = [.. processes];

        public Task<WorkspaceStaffAccessProcess?> GetAsync(
            Guid processId,
            CancellationToken cancellationToken) =>
            Task.FromResult(this.Processes.SingleOrDefault(process => process.Id == processId));

        public Task<WorkspaceStaffAccessProcess?> GetByStaffVersionAsync(
            Guid staffMemberId,
            long targetStaffVersion,
            CancellationToken cancellationToken) => Task.FromResult(this.Processes.SingleOrDefault(
            process => process.StaffMemberId == staffMemberId &&
                process.TargetStaffVersion == targetStaffVersion));

        public Task<WorkspaceStaffAccessProcess?> GetOpenByStaffAsync(
            Guid staffMemberId,
            CancellationToken cancellationToken) => Task.FromResult(this.Processes.SingleOrDefault(
            process => process.StaffMemberId == staffMemberId &&
                process.State != WorkspaceStaffAccessProcessState.Completed));

        public Task<WorkspaceStaffAccessProcess?> GetLatestCompletedSuspensionAsync(
            Guid staffMemberId,
            string subjectId,
            CancellationToken cancellationToken)
        {
            if (this.FailLatestSuspensionRead)
            {
                throw new InvalidOperationException("The persistence read must not be used for an observed commit.");
            }

            return Task.FromResult(this.Processes
                .Where(process => process.StaffMemberId == staffMemberId &&
                    process.SubjectId == subjectId &&
                    process.TargetState == WorkspaceStaffAccessTargetState.Suspended &&
                    process.State == WorkspaceStaffAccessProcessState.Completed)
                .OrderByDescending(process => process.TargetStaffVersion)
                .FirstOrDefault());
        }

        public Task<WorkspaceStaffAccessProcessListResponse> ListOpenAsync(
            PageRequest page,
            CancellationToken cancellationToken) => Task.FromResult(
            new WorkspaceStaffAccessProcessListResponse(
                [],
                page.Page,
                page.PageSize));

        public Task AddAsync(
            WorkspaceStaffAccessProcess process,
            CancellationToken cancellationToken)
        {
            this.Processes.Add(process);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeMembershipLifecycle(
        List<string> operations,
        OrganizationMembershipLifecycleOutcome outcome = OrganizationMembershipLifecycleOutcome.Changed)
        : IOrganizationMembershipLifecycle
    {
        public Task<OrganizationMembershipLifecycleResult> EnsureStateAsync(
            Guid organizationId,
            string subjectId,
            OrganizationMembershipStatus desiredStatus,
            string actorId,
            CancellationToken cancellationToken = default)
        {
            operations.Add($"membership:{desiredStatus}");
            return Task.FromResult(new OrganizationMembershipLifecycleResult(outcome, null));
        }
    }

    private sealed class FakeRoles(List<string> operations) : IAccessControlRoleProvisioner
    {
        private readonly HashSet<(AccessSubjectKind Kind, string Subject, string Role, string Scope)> assignments = [];

        public void Add(AccessSubject subject, string role, AccessScope scope) =>
            this.assignments.Add((subject.Kind, subject.Id, role, scope.Value));

        public bool Has(AccessSubject subject, string role, AccessScope scope) =>
            this.assignments.Contains((subject.Kind, subject.Id, role, scope.Value));

        public Task EnsureRoleAsync(
            AccessControlRoleDefinition role,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task EnsureAssignmentAsync(
            AccessSubject subject,
            string roleName,
            AccessScope scope,
            CancellationToken cancellationToken = default)
        {
            this.Add(subject, roleName, scope);
            operations.Add($"role:assign:{roleName}");
            return Task.CompletedTask;
        }

        public Task<AccessControlAssignmentRemovalOutcome> RemoveAssignmentAsync(
            AccessSubject subject,
            string roleName,
            AccessScope scope,
            CancellationToken cancellationToken = default)
        {
            bool removed = this.assignments.Remove((subject.Kind, subject.Id, roleName, scope.Value));
            operations.Add($"role:remove:{roleName}");
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
            CancellationToken cancellationToken = default) => Task.FromResult(
            new AccessControlPage<AccessControlRoleAssignment>([], page, pageSize, false));
    }

    private sealed class FakeProfiles(List<string> operations) : IAccessProfileProvisioner
    {
        private readonly Dictionary<Guid, AccessProfileDto> profiles = [];
        private readonly Dictionary<(AccessSubjectKind Kind, string Subject, string Scope), HashSet<Guid>> assignments = [];

        public AccessProfileDto AddProfile(AccessScope scope, string key)
        {
            AccessProfileDto profile = new(
                Guid.NewGuid(), scope.Value, key, key, string.Empty, AccessProfileStatus.Active,
                1, [], 0, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);
            this.profiles[profile.Id] = profile;
            return profile;
        }

        public void Assign(AccessSubject subject, AccessScope scope, Guid profileId) =>
            this.GetAssignments(subject, scope).Add(profileId);

        public Guid[] AssignedProfileIds(AccessSubject subject, AccessScope scope) =>
            this.GetAssignments(subject, scope).ToArray();

        public Task<AccessProfileDto> EnsureProfileAsync(
            AccessScope ownerScope,
            AccessProfileDefinition definition,
            AccessSubject actor,
            CancellationToken cancellationToken = default)
        {
            AccessProfileDto? existing = this.profiles.Values.SingleOrDefault(
                profile => profile.OwnerScope == ownerScope.Value && profile.Key == definition.Key);
            return Task.FromResult(existing ?? this.AddProfile(ownerScope, definition.Key));
        }

        public Task<AccessProfileDto?> FindProfileByKeyAsync(
            AccessScope ownerScope,
            string key,
            CancellationToken cancellationToken = default) => Task.FromResult(this.profiles.Values
            .SingleOrDefault(profile => profile.OwnerScope == ownerScope.Value && profile.Key == key));

        public Task<AccessProfileAssignmentSet> GetSubjectAssignmentsAsync(
            AccessSubject subject,
            AccessScope ownerScope,
            CancellationToken cancellationToken = default)
        {
            HashSet<Guid> ids = this.GetAssignments(subject, ownerScope);
            return Task.FromResult(new AccessProfileAssignmentSet(
                subject,
                ownerScope,
                this.profiles.Values.Where(profile => ids.Contains(profile.Id)).ToArray()));
        }

        public Task<AccessProfileAssignmentReconciliation> ReconcileSubjectAssignmentsAsync(
            AccessSubject subject,
            AccessScope ownerScope,
            IReadOnlyCollection<Guid> profileIds,
            AccessSubject actor,
            CancellationToken cancellationToken = default)
        {
            operations.Add("profiles:reconcile");
            HashSet<Guid> current = this.GetAssignments(subject, ownerScope);
            int added = profileIds.Count(profileId => !current.Contains(profileId));
            int removed = current.Count(profileId => !profileIds.Contains(profileId));
            current.Clear();
            current.UnionWith(profileIds);
            return Task.FromResult(new AccessProfileAssignmentReconciliation(
                subject, ownerScope, profileIds.ToArray(), added, removed));
        }

        private HashSet<Guid> GetAssignments(AccessSubject subject, AccessScope scope)
        {
            var key = (subject.Kind, subject.Id, scope.Value);
            if (!this.assignments.TryGetValue(key, out HashSet<Guid>? current))
            {
                current = [];
                this.assignments[key] = current;
            }

            return current;
        }
    }

    private sealed class TestClock : ISystemClock
    {
        public DateTimeOffset UtcNow => Now;
    }
}

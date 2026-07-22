namespace BunkFy.Modules.Workspaces.Tests.Application;

using BunkFy.Modules.Workspaces.Application;
using BunkFy.Modules.Workspaces.Application.Ports;
using BunkFy.Modules.Workspaces.Contracts;
using BunkFy.Modules.Workspaces.Domain;
using Gma.Framework.Results;
using Gma.Framework.Scoping;
using Gma.Modules.Organizations.Contracts;
using Xunit;

[Trait("Category", "Unit")]
public sealed class WorkspaceStaffJoinSourceManagerTests
{
    private static readonly Guid WorkspaceId = Guid.Parse("d951fcb1-d6fc-47f8-bb47-2a7a9325ab2c");
    private static readonly DateTimeOffset Now = new(2026, 7, 22, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Invitation_list_uses_one_batch_plan_read_and_keeps_unmanaged_sources_visible()
    {
        Guid managedId = Guid.NewGuid();
        Guid unmanagedId = Guid.NewGuid();
        RecordingOrganizations organizations = new()
        {
            Invitations = new OrganizationInvitationListResponse(
            [
                Invitation(managedId),
                Invitation(unmanagedId)
            ],
            2,
            25)
        };
        RecordingPlans plans = new(Plan(managedId, WorkspaceStaffOnboardingSource.Invitation));
        WorkspaceStaffJoinSourceManager manager = CreateManager(organizations, plans);

        Result<WorkspaceStaffJoinSourceListResponse> result = await manager.ListAsync(
            WorkspaceStaffOnboardingSourceKind.Invitation,
            2,
            25,
            "owner-a");

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Page);
        Assert.Equal(2, result.Value.Items.Count);
        Assert.NotNull(result.Value.Items.Single(item => item.SourceId == managedId).AccessPlan);
        Assert.Null(result.Value.Items.Single(item => item.SourceId == unmanagedId).AccessPlan);
        Assert.Equal([managedId, unmanagedId], Assert.Single(plans.BatchReads));
    }

    [Fact]
    public async Task Enrollment_list_maps_capacity_and_plan_summary()
    {
        Guid sourceId = Guid.NewGuid();
        RecordingOrganizations organizations = new()
        {
            EnrollmentLinks = new OrganizationEnrollmentLinkListResponse(
            [
                new OrganizationEnrollmentLinkDto(
                    sourceId,
                    WorkspaceId,
                    "owner-a",
                    Now.AddDays(1),
                    12,
                    12,
                    OrganizationEnrollmentApprovalMode.RequiresApproval,
                    OrganizationEnrollmentLinkStatus.CapacityReached,
                    5,
                    Now,
                    Now)
            ],
            1,
            25)
        };
        WorkspaceStaffJoinSourceManager manager = CreateManager(
            organizations,
            new RecordingPlans(Plan(sourceId, WorkspaceStaffOnboardingSource.EnrollmentLink)));

        Result<WorkspaceStaffJoinSourceListResponse> result = await manager.ListAsync(
            WorkspaceStaffOnboardingSourceKind.EnrollmentLink,
            1,
            25,
            "owner-a");

        WorkspaceStaffJoinSourceDto source = Assert.Single(result.Value.Items);
        Assert.Equal(WorkspaceStaffJoinSourceStatus.CapacityReached, source.Status);
        Assert.Equal(12, source.MaximumClaims);
        Assert.Equal("RequiresApproval", source.ApprovalMode);
        Assert.Equal("front-desk", source.AccessPlan!.ProfileKey);
    }

    [Fact]
    public async Task Revocation_forwards_scope_subject_actor_and_expected_version()
    {
        Guid sourceId = Guid.NewGuid();
        RecordingOrganizations organizations = new();
        RecordingPlans plans = new(Plan(sourceId, WorkspaceStaffOnboardingSource.Invitation));
        WorkspaceStaffJoinSourceManager manager = CreateManager(organizations, plans);

        Result<WorkspaceStaffJoinSourceDto> result = await manager.RevokeInvitationAsync(
            sourceId,
            7,
            " owner-a ");

        Assert.True(result.IsSuccess);
        OrganizationInvitationRevocationRequest request = Assert.Single(organizations.Revocations);
        Assert.Equal(WorkspaceId, request.OrganizationId);
        Assert.Equal(sourceId, request.InvitationId);
        Assert.Equal(7, request.ExpectedVersion);
        Assert.Equal("owner-a", request.SubjectId);
        Assert.Equal("owner-a", request.ActorId);
        Assert.Equal(WorkspaceStaffJoinSourceStatus.Revoked, result.Value.Status);
    }

    [Theory]
    [InlineData(0, 25)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task Invalid_paging_fails_before_organizations_call(int page, int pageSize)
    {
        RecordingOrganizations organizations = new();
        WorkspaceStaffJoinSourceManager manager = CreateManager(
            organizations,
            new RecordingPlans());

        Result<WorkspaceStaffJoinSourceListResponse> result = await manager.ListAsync(
            WorkspaceStaffOnboardingSourceKind.Invitation,
            page,
            pageSize,
            "owner-a");

        Assert.Equal(WorkspaceAccessManagementErrors.JoinSourceRequestInvalid, result.Error);
        Assert.Equal(0, organizations.ListCallCount);
    }

    private static WorkspaceStaffJoinSourceManager CreateManager(
        RecordingOrganizations organizations,
        RecordingPlans plans) => new(
            organizations,
            plans,
            new StubScopeContext(WorkspaceId.ToString("D")));

    private static OrganizationInvitationDto Invitation(Guid sourceId) => new(
        sourceId,
        WorkspaceId,
        "owner-a",
        "staff@example.test",
        Now.AddDays(1),
        OrganizationInvitationStatus.Pending,
        null,
        null,
        3,
        Now,
        Now);

    private static WorkspaceStaffAccessPlan Plan(
        Guid sourceId,
        WorkspaceStaffOnboardingSource sourceKind)
    {
        Result<WorkspaceStaffAccessPlan> created = WorkspaceStaffAccessPlan.Create(
            sourceId,
            WorkspaceId.ToString("D"),
            sourceKind,
            Guid.NewGuid(),
            "front-desk",
            [],
            "owner-a",
            Now);
        Assert.True(created.IsSuccess);
        Assert.True(created.Value.Activate(Now.AddMinutes(1)).IsSuccess);
        return created.Value;
    }

    private sealed class RecordingOrganizations : IOrganizationJoinSourceManager
    {
        public OrganizationInvitationListResponse Invitations { get; init; } = new([], 1, 25);
        public OrganizationEnrollmentLinkListResponse EnrollmentLinks { get; init; } = new([], 1, 25);
        public List<OrganizationInvitationRevocationRequest> Revocations { get; } = [];
        public int ListCallCount { get; private set; }

        public Task<OrganizationJoinSourceOperation<OrganizationInvitationDto>> GetInvitationAsync(
            OrganizationJoinSourceLookupRequest request,
            CancellationToken cancellationToken = default) => Task.FromResult(
                new OrganizationJoinSourceOperation<OrganizationInvitationDto>(
                    Invitation(request.SourceId),
                    null));

        public Task<OrganizationJoinSourceOperation<OrganizationEnrollmentLinkDto>> GetEnrollmentLinkAsync(
            OrganizationJoinSourceLookupRequest request,
            CancellationToken cancellationToken = default) => Task.FromResult(
                new OrganizationJoinSourceOperation<OrganizationEnrollmentLinkDto>(
                    new OrganizationEnrollmentLinkDto(
                        request.SourceId,
                        request.OrganizationId,
                        request.SubjectId,
                        Now.AddDays(1),
                        10,
                        0,
                        OrganizationEnrollmentApprovalMode.Automatic,
                        OrganizationEnrollmentLinkStatus.Active,
                        1,
                        Now,
                        Now),
                    null));

        public Task<OrganizationJoinSourceOperation<OrganizationInvitationListResponse>> ListInvitationsAsync(
            OrganizationJoinSourceListRequest request,
            CancellationToken cancellationToken = default)
        {
            this.ListCallCount++;
            return Task.FromResult(new OrganizationJoinSourceOperation<OrganizationInvitationListResponse>(
                this.Invitations,
                null));
        }

        public Task<OrganizationJoinSourceOperation<OrganizationEnrollmentLinkListResponse>> ListEnrollmentLinksAsync(
            OrganizationJoinSourceListRequest request,
            CancellationToken cancellationToken = default)
        {
            this.ListCallCount++;
            return Task.FromResult(new OrganizationJoinSourceOperation<
                OrganizationEnrollmentLinkListResponse>(this.EnrollmentLinks, null));
        }

        public Task<OrganizationJoinSourceOperation<OrganizationInvitationDto>> RevokeInvitationAsync(
            OrganizationInvitationRevocationRequest request,
            CancellationToken cancellationToken = default)
        {
            this.Revocations.Add(request);
            return Task.FromResult(new OrganizationJoinSourceOperation<OrganizationInvitationDto>(
                Invitation(request.InvitationId) with
                {
                    Status = OrganizationInvitationStatus.Revoked,
                    Version = request.ExpectedVersion + 1
                },
                null));
        }

        public Task<OrganizationJoinSourceOperation<OrganizationEnrollmentLinkDto>> DisableEnrollmentLinkAsync(
            OrganizationEnrollmentLinkDisableRequest request,
            CancellationToken cancellationToken = default) => Task.FromResult(
                new OrganizationJoinSourceOperation<OrganizationEnrollmentLinkDto>(
                    new OrganizationEnrollmentLinkDto(
                        request.EnrollmentLinkId,
                        request.OrganizationId,
                        request.SubjectId,
                        Now.AddDays(1),
                        10,
                        0,
                        OrganizationEnrollmentApprovalMode.Automatic,
                        OrganizationEnrollmentLinkStatus.Disabled,
                        request.ExpectedVersion + 1,
                        Now,
                        Now),
                    null));
    }

    private sealed class RecordingPlans(params WorkspaceStaffAccessPlan[] seed)
        : IWorkspaceStaffAccessPlanRepository
    {
        private readonly Dictionary<Guid, WorkspaceStaffAccessPlan> plans = seed.ToDictionary(
            plan => plan.Id);

        public List<IReadOnlyList<Guid>> BatchReads { get; } = [];

        public Task<WorkspaceStaffAccessPlan?> GetAsync(
            Guid sourceId,
            CancellationToken cancellationToken) => Task.FromResult(
                this.plans.GetValueOrDefault(sourceId));

        public Task<IReadOnlyDictionary<Guid, WorkspaceStaffAccessPlan>> GetManyAsync(
            IReadOnlyCollection<Guid> sourceIds,
            CancellationToken cancellationToken)
        {
            this.BatchReads.Add(sourceIds.ToArray());
            return Task.FromResult<IReadOnlyDictionary<Guid, WorkspaceStaffAccessPlan>>(
                this.plans.Where(item => sourceIds.Contains(item.Key)).ToDictionary());
        }

        public Task AddAsync(
            WorkspaceStaffAccessPlan plan,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class StubScopeContext(string scopeId) : IScopeContext
    {
        public bool IsEnabled => true;
        public string? ScopeId => scopeId;
    }
}

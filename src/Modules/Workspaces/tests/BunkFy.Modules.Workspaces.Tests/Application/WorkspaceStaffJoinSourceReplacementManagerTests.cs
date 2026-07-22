namespace BunkFy.Modules.Workspaces.Tests;

using BunkFy.Modules.Workspaces.Application;
using BunkFy.Modules.Workspaces.Application.Ports;
using BunkFy.Modules.Workspaces.Contracts;
using BunkFy.Modules.Workspaces.Domain;
using Gma.Framework.Results;
using Gma.Framework.Scoping;
using Gma.Modules.Organizations.Contracts;
using Xunit;

[Trait("Category", "Unit")]
public sealed class WorkspaceStaffJoinSourceReplacementManagerTests
{
    private static readonly Guid WorkspaceId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 7, 22, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Pending_invitation_is_denied_before_replacement_uses_the_same_plan()
    {
        Guid sourceId = Guid.NewGuid();
        Guid replacementId = Guid.NewGuid();
        Guid propertyId = Guid.NewGuid();
        List<string> order = [];
        RecordingOrganizations organizations = new(order)
        {
            Invitation = Invitation(sourceId, OrganizationInvitationStatus.Pending, 4)
        };
        RecordingIssuer issuer = new(order);
        WorkspaceStaffJoinSourceReplacementManager manager = CreateManager(
            organizations,
            new RecordingPlans(Plan(sourceId, WorkspaceStaffOnboardingSource.Invitation, propertyId)),
            issuer);

        Result<WorkspaceStaffJoinSourceReplacementDto> result = await manager.ReplaceInvitationAsync(
            sourceId, replacementId, 4, 48, " owner-a ");

        Assert.True(result.IsSuccess, result.Error.Code);
        Assert.Equal(["get.invitation", "revoke", "issue.invitation"], order);
        OrganizationInvitationRevocationRequest revoked = Assert.Single(organizations.Revocations);
        Assert.Equal(WorkspaceId, revoked.OrganizationId);
        Assert.Equal(4, revoked.ExpectedVersion);
        WorkspaceInvitationIssuanceRequest issued = Assert.Single(issuer.Invitations);
        Assert.Equal(replacementId, issued.SourceId);
        Assert.Equal("staff@example.test", issued.RecipientEmail);
        Assert.Equal(WorkspaceAccessProfileSeeds.FrontDeskKey, issued.ProfileKey);
        Assert.Equal([propertyId], issued.PropertyIds);
        Assert.Equal(WorkspaceStaffJoinSourceStatus.Revoked, result.Value.PreviousStatus);
        Assert.Equal(5, result.Value.PreviousVersion);
    }

    [Fact]
    public async Task Retry_after_revocation_skips_denial_and_does_not_replay_the_token()
    {
        Guid sourceId = Guid.NewGuid();
        Guid replacementId = Guid.NewGuid();
        List<string> order = [];
        RecordingOrganizations organizations = new(order)
        {
            Invitation = Invitation(sourceId, OrganizationInvitationStatus.Revoked, 5)
        };
        RecordingIssuer issuer = new(order) { AlreadyIssued = true };
        WorkspaceStaffJoinSourceReplacementManager manager = CreateManager(
            organizations,
            new RecordingPlans(Plan(sourceId, WorkspaceStaffOnboardingSource.Invitation)),
            issuer);

        Result<WorkspaceStaffJoinSourceReplacementDto> result = await manager.ReplaceInvitationAsync(
            sourceId, replacementId, 4, 48, "owner-a");

        Assert.True(result.IsSuccess, result.Error.Code);
        Assert.Equal(["get.invitation", "issue.invitation"], order);
        Assert.Empty(organizations.Revocations);
        Assert.True(result.Value.Replacement.AlreadyIssued);
        Assert.Null(result.Value.Replacement.Token);
    }

    [Fact]
    public async Task Denial_failure_never_issues_a_replacement()
    {
        Guid sourceId = Guid.NewGuid();
        List<string> order = [];
        RecordingOrganizations organizations = new(order)
        {
            Invitation = Invitation(sourceId, OrganizationInvitationStatus.Pending, 4),
            DenialError = "Organizations.VersionConflict"
        };
        RecordingIssuer issuer = new(order);
        WorkspaceStaffJoinSourceReplacementManager manager = CreateManager(
            organizations,
            new RecordingPlans(Plan(sourceId, WorkspaceStaffOnboardingSource.Invitation)),
            issuer);

        Result<WorkspaceStaffJoinSourceReplacementDto> result = await manager.ReplaceInvitationAsync(
            sourceId, Guid.NewGuid(), 3, 48, "owner-a");

        Assert.Equal(WorkspaceAccessManagementErrors.JoinSourceManagementFailed, result.Error);
        Assert.Equal(["get.invitation", "revoke"], order);
        Assert.Empty(issuer.Invitations);
    }

    [Fact]
    public async Task Capacity_reached_link_is_disabled_before_replacement_copies_configuration()
    {
        Guid sourceId = Guid.NewGuid();
        Guid replacementId = Guid.NewGuid();
        List<string> order = [];
        RecordingOrganizations organizations = new(order)
        {
            EnrollmentLink = EnrollmentLink(
                sourceId,
                OrganizationEnrollmentLinkStatus.CapacityReached,
                6)
        };
        RecordingIssuer issuer = new(order);
        WorkspaceStaffJoinSourceReplacementManager manager = CreateManager(
            organizations,
            new RecordingPlans(Plan(sourceId, WorkspaceStaffOnboardingSource.EnrollmentLink)),
            issuer);

        Result<WorkspaceStaffJoinSourceReplacementDto> result =
            await manager.ReplaceEnrollmentLinkAsync(
                sourceId, replacementId, 6, 72, "owner-a");

        Assert.True(result.IsSuccess, result.Error.Code);
        Assert.Equal(["get.enrollment", "disable", "issue.enrollment"], order);
        WorkspaceEnrollmentLinkIssuanceRequest issued = Assert.Single(issuer.EnrollmentLinks);
        Assert.Equal(replacementId, issued.SourceId);
        Assert.Equal(20, issued.MaximumClaims);
        Assert.Equal(OrganizationEnrollmentApprovalMode.RequiresApproval, issued.ApprovalMode);
        Assert.Equal(WorkspaceStaffJoinSourceStatus.Disabled, result.Value.PreviousStatus);
    }

    [Fact]
    public async Task Invalid_or_unmanaged_replacement_fails_before_source_state_is_read()
    {
        Guid sourceId = Guid.NewGuid();
        List<string> order = [];
        RecordingOrganizations organizations = new(order);
        RecordingIssuer issuer = new(order);
        WorkspaceStaffJoinSourceReplacementManager manager = CreateManager(
            organizations,
            new RecordingPlans(),
            issuer);

        Result<WorkspaceStaffJoinSourceReplacementDto> invalid = await manager.ReplaceInvitationAsync(
            sourceId, sourceId, 1, 24, "owner-a");
        Result<WorkspaceStaffJoinSourceReplacementDto> unmanaged = await manager.ReplaceInvitationAsync(
            sourceId, Guid.NewGuid(), 1, 24, "owner-a");

        Assert.Equal(WorkspaceAccessManagementErrors.JoinSourceRequestInvalid, invalid.Error);
        Assert.Equal(WorkspaceAccessManagementErrors.JoinSourcePlanUnavailable, unmanaged.Error);
        Assert.Empty(order);
    }

    private static WorkspaceStaffJoinSourceReplacementManager CreateManager(
        RecordingOrganizations organizations,
        RecordingPlans plans,
        RecordingIssuer issuer) => new(
            organizations,
            plans,
            issuer,
            new StubScopeContext(WorkspaceId.ToString("D")));

    private static OrganizationInvitationDto Invitation(
        Guid sourceId,
        OrganizationInvitationStatus status,
        long version) => new(
            sourceId,
            WorkspaceId,
            "owner-a",
            "staff@example.test",
            Now.AddDays(1),
            status,
            null,
            null,
            version,
            Now,
            Now);

    private static OrganizationEnrollmentLinkDto EnrollmentLink(
        Guid sourceId,
        OrganizationEnrollmentLinkStatus status,
        long version) => new(
            sourceId,
            WorkspaceId,
            "owner-a",
            Now.AddDays(1),
            20,
            20,
            OrganizationEnrollmentApprovalMode.RequiresApproval,
            status,
            version,
            Now,
            Now);

    private static WorkspaceStaffAccessPlan Plan(
        Guid sourceId,
        WorkspaceStaffOnboardingSource sourceKind,
        params Guid[] propertyIds)
    {
        Result<WorkspaceStaffAccessPlan> created = WorkspaceStaffAccessPlan.Create(
            sourceId,
            WorkspaceId.ToString("D"),
            sourceKind,
            Guid.NewGuid(),
            WorkspaceAccessProfileSeeds.FrontDeskKey,
            propertyIds,
            "owner-a",
            Now);
        Assert.True(created.IsSuccess);
        Assert.True(created.Value.Activate(Now.AddMinutes(1)).IsSuccess);
        return created.Value;
    }

    private sealed class RecordingOrganizations(List<string> order) : IOrganizationJoinSourceManager
    {
        public OrganizationInvitationDto? Invitation { get; init; }
        public OrganizationEnrollmentLinkDto? EnrollmentLink { get; init; }
        public string? DenialError { get; init; }
        public List<OrganizationInvitationRevocationRequest> Revocations { get; } = [];

        public Task<OrganizationJoinSourceOperation<OrganizationInvitationDto>> GetInvitationAsync(
            OrganizationJoinSourceLookupRequest request,
            CancellationToken cancellationToken = default)
        {
            order.Add("get.invitation");
            return Task.FromResult(new OrganizationJoinSourceOperation<OrganizationInvitationDto>(
                this.Invitation,
                this.Invitation is null ? "Organizations.InvitationNotFound" : null));
        }

        public Task<OrganizationJoinSourceOperation<OrganizationEnrollmentLinkDto>> GetEnrollmentLinkAsync(
            OrganizationJoinSourceLookupRequest request,
            CancellationToken cancellationToken = default)
        {
            order.Add("get.enrollment");
            return Task.FromResult(new OrganizationJoinSourceOperation<OrganizationEnrollmentLinkDto>(
                this.EnrollmentLink,
                this.EnrollmentLink is null ? "Organizations.EnrollmentLinkNotFound" : null));
        }

        public Task<OrganizationJoinSourceOperation<OrganizationInvitationDto>> RevokeInvitationAsync(
            OrganizationInvitationRevocationRequest request,
            CancellationToken cancellationToken = default)
        {
            order.Add("revoke");
            this.Revocations.Add(request);
            return Task.FromResult(this.DenialError is null
                ? new OrganizationJoinSourceOperation<OrganizationInvitationDto>(
                    this.Invitation! with
                    {
                        Status = OrganizationInvitationStatus.Revoked,
                        Version = request.ExpectedVersion + 1
                    },
                    null)
                : new OrganizationJoinSourceOperation<OrganizationInvitationDto>(
                    null,
                    this.DenialError));
        }

        public Task<OrganizationJoinSourceOperation<OrganizationEnrollmentLinkDto>> DisableEnrollmentLinkAsync(
            OrganizationEnrollmentLinkDisableRequest request,
            CancellationToken cancellationToken = default)
        {
            order.Add("disable");
            return Task.FromResult(this.DenialError is null
                ? new OrganizationJoinSourceOperation<OrganizationEnrollmentLinkDto>(
                    this.EnrollmentLink! with
                    {
                        Status = OrganizationEnrollmentLinkStatus.Disabled,
                        Version = request.ExpectedVersion + 1
                    },
                    null)
                : new OrganizationJoinSourceOperation<OrganizationEnrollmentLinkDto>(
                    null,
                    this.DenialError));
        }

        public Task<OrganizationJoinSourceOperation<OrganizationInvitationListResponse>> ListInvitationsAsync(
            OrganizationJoinSourceListRequest request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<OrganizationJoinSourceOperation<OrganizationEnrollmentLinkListResponse>> ListEnrollmentLinksAsync(
            OrganizationJoinSourceListRequest request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class RecordingIssuer(List<string> order) : IWorkspaceStaffJoinSourceIssuer
    {
        public bool AlreadyIssued { get; init; }
        public List<WorkspaceInvitationIssuanceRequest> Invitations { get; } = [];
        public List<WorkspaceEnrollmentLinkIssuanceRequest> EnrollmentLinks { get; } = [];

        public Task<Result<WorkspaceStaffJoinSourceIssuanceDto>> IssueInvitationAsync(
            WorkspaceInvitationIssuanceRequest request,
            CancellationToken cancellationToken = default)
        {
            order.Add("issue.invitation");
            this.Invitations.Add(request);
            return Task.FromResult(Result.Success(Issuance(
                request.SourceId,
                WorkspaceStaffOnboardingSourceKind.Invitation,
                request.ProfileKey,
                request.PropertyIds,
                this.AlreadyIssued)));
        }

        public Task<Result<WorkspaceStaffJoinSourceIssuanceDto>> IssueEnrollmentLinkAsync(
            WorkspaceEnrollmentLinkIssuanceRequest request,
            CancellationToken cancellationToken = default)
        {
            order.Add("issue.enrollment");
            this.EnrollmentLinks.Add(request);
            return Task.FromResult(Result.Success(Issuance(
                request.SourceId,
                WorkspaceStaffOnboardingSourceKind.EnrollmentLink,
                request.ProfileKey,
                request.PropertyIds,
                this.AlreadyIssued)));
        }

        private static WorkspaceStaffJoinSourceIssuanceDto Issuance(
            Guid sourceId,
            WorkspaceStaffOnboardingSourceKind sourceKind,
            string profileKey,
            IReadOnlyCollection<Guid> propertyIds,
            bool alreadyIssued) => new(
                new WorkspaceStaffAccessPlanDto(
                    sourceId,
                    sourceKind,
                    Guid.NewGuid(),
                    profileKey,
                    propertyIds,
                    WorkspaceStaffAccessPlanStatus.Active,
                    2,
                    Now,
                    Now),
                alreadyIssued ? null : "one-time-token",
                alreadyIssued);
    }

    private sealed class RecordingPlans(params WorkspaceStaffAccessPlan[] seed)
        : IWorkspaceStaffAccessPlanRepository
    {
        private readonly Dictionary<Guid, WorkspaceStaffAccessPlan> values = seed.ToDictionary(
            plan => plan.Id);

        public Task<WorkspaceStaffAccessPlan?> GetAsync(
            Guid sourceId,
            CancellationToken cancellationToken) => Task.FromResult(
                this.values.GetValueOrDefault(sourceId));

        public Task<IReadOnlyDictionary<Guid, WorkspaceStaffAccessPlan>> GetManyAsync(
            IReadOnlyCollection<Guid> sourceIds,
            CancellationToken cancellationToken) => throw new NotSupportedException();

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

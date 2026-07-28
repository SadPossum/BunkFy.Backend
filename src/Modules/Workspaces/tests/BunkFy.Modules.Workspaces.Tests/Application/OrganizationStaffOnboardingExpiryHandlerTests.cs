namespace BunkFy.Modules.Workspaces.Tests;

using BunkFy.Modules.Workspaces.Application.Handlers;
using BunkFy.Modules.Workspaces.Application.Ports;
using BunkFy.Modules.Workspaces.Contracts;
using BunkFy.Modules.Workspaces.Domain;
using Gma.Framework.Pagination;
using Gma.Framework.Runtime.Time;
using Gma.Modules.Organizations.Contracts;
using Xunit;

[Trait("Category", "Unit")]
public sealed class OrganizationStaffOnboardingExpiryHandlerTests
{
    [Fact]
    public async Task Invitation_expiry_terminates_the_application_and_access_plan()
    {
        WorkspaceStaffOnboarding application = WorkspaceStaffOnboardingTests.CreateApplication(
            WorkspaceStaffOnboardingSource.Invitation);
        WorkspaceStaffAccessPlan plan = CreateActivePlan(
            WorkspaceStaffOnboardingSource.Invitation,
            application.SourceId);
        FakeOnboardingRepository applications = new(application);
        OrganizationInvitationExpiredStaffOnboardingHandler handler = new(
            applications,
            new FakeAccessPlanRepository(plan),
            new FakeClock());

        await handler.HandleAsync(
            new OrganizationInvitationExpiredIntegrationEvent(
                Guid.NewGuid(),
                Now.AddMinutes(1),
                ScopeId,
                OrganizationId,
                application.SourceId,
                Now,
                2),
            CancellationToken.None);

        Assert.Equal(WorkspaceStaffOnboardingState.Expired, application.Status);
        Assert.Equal(WorkspaceStaffAccessPlanState.Expired, plan.Status);
        Assert.Equal(Now, plan.SourceExpiredAtUtc);
        Assert.Null(application.DisplayName);
        Assert.Null(application.WorkEmail);
    }

    [Fact]
    public async Task Claim_expiry_after_link_expiry_terminates_the_application_and_plan_once()
    {
        WorkspaceStaffOnboarding application = WorkspaceStaffOnboardingTests.CreateApplication();
        Guid claimId = Guid.NewGuid();
        Assert.True(application.ObserveClaimRequested(claimId, 1, Now).IsSuccess);
        WorkspaceStaffAccessPlan plan = CreateActivePlan(
            WorkspaceStaffOnboardingSource.EnrollmentLink,
            application.SourceId);
        FakeOnboardingRepository applications = new(application);
        FakeAccessPlanRepository plans = new(plan);
        OrganizationEnrollmentLinkExpiredStaffOnboardingHandler linkHandler = new(
            applications,
            plans,
            new FakeClock());
        await linkHandler.HandleAsync(
            new OrganizationEnrollmentLinkExpiredIntegrationEvent(
                Guid.NewGuid(),
                Now.AddMinutes(1),
                ScopeId,
                OrganizationId,
                application.SourceId,
                Now,
                2),
            CancellationToken.None);

        OrganizationEnrollmentClaimExpiredStaffOnboardingHandler handler = new(
            applications,
            plans,
            new FakeClock());
        OrganizationEnrollmentClaimExpiredIntegrationEvent integrationEvent = new(
            Guid.NewGuid(),
            Now.AddMinutes(1),
            ScopeId,
            OrganizationId,
            application.SourceId,
            claimId,
            Now,
            2);

        await handler.HandleAsync(integrationEvent, CancellationToken.None);
        long applicationVersion = application.Version;
        long planVersion = plan.Version;
        await handler.HandleAsync(integrationEvent, CancellationToken.None);

        Assert.Equal(WorkspaceStaffOnboardingState.Expired, application.Status);
        Assert.Equal(WorkspaceStaffAccessPlanState.Expired, plan.Status);
        Assert.Equal(Now, plan.SourceExpiredAtUtc);
        Assert.Equal(applicationVersion, application.Version);
        Assert.Equal(planVersion, plan.Version);
        Assert.Null(application.VerifiedAccountEmail);
        Assert.Null(application.DisplayName);
    }

    [Fact]
    public async Task Claim_expiry_preserves_the_reusable_plan_while_its_link_is_active()
    {
        WorkspaceStaffOnboarding application = WorkspaceStaffOnboardingTests.CreateApplication();
        Guid claimId = Guid.NewGuid();
        Assert.True(application.ObserveClaimRequested(claimId, 1, Now).IsSuccess);
        WorkspaceStaffAccessPlan plan = CreateActivePlan(
            WorkspaceStaffOnboardingSource.EnrollmentLink,
            application.SourceId);
        OrganizationEnrollmentClaimExpiredStaffOnboardingHandler handler = new(
            new FakeOnboardingRepository(application),
            new FakeAccessPlanRepository(plan),
            new FakeClock());

        await handler.HandleAsync(
            new OrganizationEnrollmentClaimExpiredIntegrationEvent(
                Guid.NewGuid(),
                Now.AddMinutes(1),
                ScopeId,
                OrganizationId,
                application.SourceId,
                claimId,
                Now,
                2),
            CancellationToken.None);

        Assert.Equal(WorkspaceStaffOnboardingState.Expired, application.Status);
        Assert.Equal(WorkspaceStaffAccessPlanState.Active, plan.Status);
        Assert.Null(plan.SourceExpiredAtUtc);
    }

    [Fact]
    public async Task Claim_expiry_without_its_application_is_retried_by_the_inbox()
    {
        OrganizationEnrollmentClaimExpiredStaffOnboardingHandler handler = new(
            new FakeOnboardingRepository(),
            new FakeAccessPlanRepository(),
            new FakeClock());

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(
            new OrganizationEnrollmentClaimExpiredIntegrationEvent(
                Guid.NewGuid(),
                Now.AddMinutes(1),
                ScopeId,
                OrganizationId,
                Guid.NewGuid(),
                Guid.NewGuid(),
                Now,
                1),
            CancellationToken.None));
    }

    [Fact]
    public async Task Link_expiry_preserves_a_plan_while_a_pending_claim_uses_it()
    {
        WorkspaceStaffOnboarding application = WorkspaceStaffOnboardingTests.CreateApplication();
        Assert.True(application.ObserveClaimRequested(Guid.NewGuid(), 1, Now).IsSuccess);
        WorkspaceStaffAccessPlan plan = CreateActivePlan(
            WorkspaceStaffOnboardingSource.EnrollmentLink,
            application.SourceId);
        OrganizationEnrollmentLinkExpiredStaffOnboardingHandler handler = new(
            new FakeOnboardingRepository(application),
            new FakeAccessPlanRepository(plan),
            new FakeClock());

        await handler.HandleAsync(
            new OrganizationEnrollmentLinkExpiredIntegrationEvent(
                Guid.NewGuid(),
                Now.AddMinutes(1),
                ScopeId,
                OrganizationId,
                application.SourceId,
                Now,
                2),
            CancellationToken.None);

        Assert.Equal(WorkspaceStaffOnboardingState.PendingApproval, application.Status);
        Assert.Equal(WorkspaceStaffAccessPlanState.Active, plan.Status);
        Assert.Equal(Now, plan.SourceExpiredAtUtc);
    }

    [Fact]
    public async Task Link_expiry_preserves_unbound_staging_for_an_out_of_order_claim_fact()
    {
        WorkspaceStaffOnboarding application = WorkspaceStaffOnboardingTests.CreateApplication();
        WorkspaceStaffAccessPlan plan = CreateActivePlan(
            WorkspaceStaffOnboardingSource.EnrollmentLink,
            application.SourceId);
        OrganizationEnrollmentLinkExpiredStaffOnboardingHandler handler = new(
            new FakeOnboardingRepository(application),
            new FakeAccessPlanRepository(plan),
            new FakeClock());

        await handler.HandleAsync(
            new OrganizationEnrollmentLinkExpiredIntegrationEvent(
                Guid.NewGuid(),
                Now.AddMinutes(1),
                ScopeId,
                OrganizationId,
                application.SourceId,
                Now,
                2),
            CancellationToken.None);
        Assert.True(application.ObserveClaimRequested(
            Guid.NewGuid(),
            1,
            Now.AddMinutes(2)).IsSuccess);

        Assert.Equal(WorkspaceStaffOnboardingState.PendingApproval, application.Status);
        Assert.Equal(WorkspaceStaffAccessPlanState.Active, plan.Status);
        Assert.Equal(Now, plan.SourceExpiredAtUtc);
    }

    [Fact]
    public async Task Link_expiry_terminates_a_plan_with_no_active_onboarding()
    {
        Guid sourceId = Guid.NewGuid();
        WorkspaceStaffAccessPlan plan = CreateActivePlan(
            WorkspaceStaffOnboardingSource.EnrollmentLink,
            sourceId);
        OrganizationEnrollmentLinkExpiredStaffOnboardingHandler handler = new(
            new FakeOnboardingRepository(),
            new FakeAccessPlanRepository(plan),
            new FakeClock());

        await handler.HandleAsync(
            new OrganizationEnrollmentLinkExpiredIntegrationEvent(
                Guid.NewGuid(),
                Now.AddMinutes(1),
                ScopeId,
                OrganizationId,
                sourceId,
                Now,
                2),
            CancellationToken.None);

        Assert.Equal(WorkspaceStaffAccessPlanState.Expired, plan.Status);
        Assert.Equal(Now, plan.SourceExpiredAtUtc);
    }

    private static WorkspaceStaffAccessPlan CreateActivePlan(
        WorkspaceStaffOnboardingSource sourceKind,
        Guid sourceId)
    {
        WorkspaceStaffAccessPlan plan = WorkspaceStaffAccessPlan.Create(
            sourceId,
            ScopeId,
            sourceKind,
            Guid.NewGuid(),
            "front-desk",
            [],
            WorkspaceStaffOnboardingTests.SubjectId,
            Now).Value;
        Assert.True(plan.Activate(Now.AddSeconds(1)).IsSuccess);
        return plan;
    }

    private static readonly DateTimeOffset Now = WorkspaceStaffOnboardingTests.Now;
    private static readonly Guid OrganizationId = WorkspaceStaffOnboardingTests.OrganizationId;
    private static readonly string ScopeId = OrganizationId.ToString("D");

    private sealed class FakeOnboardingRepository(params WorkspaceStaffOnboarding[] seed)
        : IWorkspaceStaffOnboardingRepository
    {
        private readonly List<WorkspaceStaffOnboarding> applications = [.. seed];

        public Task<WorkspaceStaffOnboarding?> GetAsync(
            Guid applicationId,
            CancellationToken cancellationToken) =>
            Task.FromResult(this.applications.SingleOrDefault(item => item.Id == applicationId));

        public Task<WorkspaceStaffOnboarding?> GetBySourceAndSubjectAsync(
            WorkspaceStaffOnboardingSource sourceKind,
            Guid sourceId,
            string subjectId,
            CancellationToken cancellationToken) =>
            Task.FromResult(this.applications.SingleOrDefault(item =>
                item.SourceKind == sourceKind &&
                item.SourceId == sourceId &&
                string.Equals(item.SubjectId, subjectId, StringComparison.Ordinal)));

        public Task<WorkspaceStaffOnboarding?> GetByClaimAsync(
            Guid claimId,
            CancellationToken cancellationToken) =>
            Task.FromResult(this.applications.SingleOrDefault(item => item.ClaimId == claimId));

        public Task<IReadOnlyList<WorkspaceStaffOnboarding>> ListActiveBySourceAsync(
            WorkspaceStaffOnboardingSource sourceKind,
            Guid sourceId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<WorkspaceStaffOnboarding>>(this.applications
                .Where(item => item.SourceKind == sourceKind && item.SourceId == sourceId)
                .ToArray());

        public Task<WorkspaceStaffOnboardingListResponse> ListActionableAsync(
            PageRequest page,
            CancellationToken cancellationToken) =>
            Task.FromResult(new WorkspaceStaffOnboardingListResponse([], page.Page, page.PageSize));

        public Task AddAsync(
            WorkspaceStaffOnboarding application,
            CancellationToken cancellationToken)
        {
            this.applications.Add(application);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeAccessPlanRepository(params WorkspaceStaffAccessPlan[] seed)
        : IWorkspaceStaffAccessPlanRepository
    {
        private readonly List<WorkspaceStaffAccessPlan> plans = [.. seed];

        public Task<WorkspaceStaffAccessPlan?> GetAsync(
            Guid sourceId,
            CancellationToken cancellationToken) =>
            Task.FromResult(this.plans.SingleOrDefault(item => item.Id == sourceId));

        public Task<IReadOnlyDictionary<Guid, WorkspaceStaffAccessPlan>> GetManyAsync(
            IReadOnlyCollection<Guid> sourceIds,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<Guid, WorkspaceStaffAccessPlan>>(this.plans
                .Where(item => sourceIds.Contains(item.Id))
                .ToDictionary(item => item.Id));

        public Task AddAsync(
            WorkspaceStaffAccessPlan plan,
            CancellationToken cancellationToken)
        {
            this.plans.Add(plan);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeClock : ISystemClock
    {
        public DateTimeOffset UtcNow => Now.AddMinutes(2);
    }
}

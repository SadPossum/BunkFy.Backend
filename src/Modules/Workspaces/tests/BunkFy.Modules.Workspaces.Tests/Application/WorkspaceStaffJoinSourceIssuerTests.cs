namespace BunkFy.Modules.Workspaces.Tests;

using BunkFy.Modules.Workspaces.Application;
using BunkFy.Modules.Workspaces.Application.Commands;
using BunkFy.Modules.Workspaces.Contracts;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Scoping;
using Gma.Modules.Organizations.Contracts;
using Xunit;

[Trait("Category", "Unit")]
public sealed class WorkspaceStaffJoinSourceIssuerTests
{
    private static readonly Guid WorkspaceId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 7, 21, 14, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Organization_failure_leaves_an_active_plan_without_a_join_source()
    {
        List<string> order = [];
        StubDispatcher dispatcher = new(CreatePlan(), order);
        StubOrganizationIssuer organizations = new(order)
        {
            InvitationResult = new OrganizationJoinSourceIssuance<OrganizationInvitationDto>(
                null,
                OrganizationJoinSourceIssuanceOutcome.Unknown,
                null,
                "Organizations.StorageUnavailable")
        };
        WorkspaceStaffJoinSourceIssuer issuer = new(
            dispatcher,
            organizations,
            new StubScopeContext(WorkspaceId.ToString("D")));

        Result<WorkspaceStaffJoinSourceIssuanceDto> result = await issuer.IssueInvitationAsync(
            InvitationRequest(dispatcher.Plan.SourceId),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(["prepare", "activate", "organizations.invitation"], order);
        Assert.Equal(WorkspaceStaffAccessPlanStatus.Active, dispatcher.Plan.Status);
        Assert.True(dispatcher.Activated);
    }

    [Fact]
    public async Task New_invitation_is_prepared_and_activated_before_token_issuance()
    {
        List<string> order = [];
        StubDispatcher dispatcher = new(CreatePlan(), order);
        StubOrganizationIssuer organizations = new(order)
        {
            InvitationResult = new OrganizationJoinSourceIssuance<OrganizationInvitationDto>(
                Invitation(dispatcher.Plan.SourceId),
                OrganizationJoinSourceIssuanceOutcome.Issued,
                "one-time-token",
                null)
        };
        WorkspaceStaffJoinSourceIssuer issuer = new(
            dispatcher,
            organizations,
            new StubScopeContext(WorkspaceId.ToString("D")));

        Result<WorkspaceStaffJoinSourceIssuanceDto> result = await issuer.IssueInvitationAsync(
            InvitationRequest(dispatcher.Plan.SourceId),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error.Code);
        Assert.Equal(["prepare", "activate", "organizations.invitation"], order);
        Assert.True(dispatcher.PreparedBeforeOrganizationCall);
        Assert.Equal(WorkspaceStaffAccessPlanStatus.Active, result.Value.Plan.Status);
        Assert.Equal("one-time-token", result.Value.Token);
        Assert.False(result.Value.AlreadyIssued);
    }

    [Fact]
    public async Task Exact_replay_activates_idempotently_without_returning_a_second_token()
    {
        List<string> order = [];
        StubDispatcher dispatcher = new(
            CreatePlan() with { Status = WorkspaceStaffAccessPlanStatus.Active, Version = 2 },
            order);
        StubOrganizationIssuer organizations = new(order)
        {
            InvitationResult = new OrganizationJoinSourceIssuance<OrganizationInvitationDto>(
                Invitation(dispatcher.Plan.SourceId),
                OrganizationJoinSourceIssuanceOutcome.AlreadyIssued,
                null,
                null)
        };
        WorkspaceStaffJoinSourceIssuer issuer = new(
            dispatcher,
            organizations,
            new StubScopeContext(WorkspaceId.ToString("D")));

        Result<WorkspaceStaffJoinSourceIssuanceDto> result = await issuer.IssueInvitationAsync(
            InvitationRequest(dispatcher.Plan.SourceId),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error.Code);
        Assert.True(result.Value.AlreadyIssued);
        Assert.Null(result.Value.Token);
        Assert.Equal(WorkspaceStaffAccessPlanStatus.Active, result.Value.Plan.Status);
        Assert.Equal(["prepare", "organizations.invitation"], order);
    }

    [Fact]
    public async Task Enrollment_link_uses_the_same_prepare_activate_issue_order()
    {
        List<string> order = [];
        WorkspaceStaffAccessPlanDto plan = CreatePlan() with
        {
            SourceKind = WorkspaceStaffOnboardingSourceKind.EnrollmentLink,
            ProfileKey = WorkspaceAccessProfileSeeds.HousekeepingKey
        };
        StubDispatcher dispatcher = new(plan, order);
        StubOrganizationIssuer organizations = new(order)
        {
            EnrollmentResult = new OrganizationJoinSourceIssuance<OrganizationEnrollmentLinkDto>(
                Enrollment(plan.SourceId),
                OrganizationJoinSourceIssuanceOutcome.Issued,
                "enrollment-token",
                null)
        };
        WorkspaceStaffJoinSourceIssuer issuer = new(
            dispatcher,
            organizations,
            new StubScopeContext(WorkspaceId.ToString("D")));

        Result<WorkspaceStaffJoinSourceIssuanceDto> result = await issuer.IssueEnrollmentLinkAsync(
            new WorkspaceEnrollmentLinkIssuanceRequest(
                plan.SourceId,
                24,
                10,
                OrganizationEnrollmentApprovalMode.RequiresApproval,
                plan.ProfileKey,
                plan.PropertyIds,
                "owner-a"),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error.Code);
        Assert.Equal(["prepare", "activate", "organizations.enrollment"], order);
        Assert.Equal("enrollment-token", result.Value.Token);
    }

    private static WorkspaceInvitationIssuanceRequest InvitationRequest(Guid sourceId) => new(
        sourceId,
        "new.staff@example.test",
        24,
        WorkspaceAccessProfileSeeds.FrontDeskKey,
        [],
        "owner-a");

    private static WorkspaceStaffAccessPlanDto CreatePlan() => new(
        Guid.NewGuid(),
        WorkspaceStaffOnboardingSourceKind.Invitation,
        Guid.NewGuid(),
        WorkspaceAccessProfileSeeds.FrontDeskKey,
        [],
        WorkspaceStaffAccessPlanStatus.Prepared,
        1,
        Now,
        Now);

    private static OrganizationInvitationDto Invitation(Guid sourceId) => new(
        sourceId,
        WorkspaceId,
        "owner-a",
        "new.staff@example.test",
        Now.AddDays(1),
        OrganizationInvitationStatus.Pending,
        null,
        null,
        1,
        Now,
        Now);

    private static OrganizationEnrollmentLinkDto Enrollment(Guid sourceId) => new(
        sourceId,
        WorkspaceId,
        "owner-a",
        Now.AddDays(1),
        10,
        0,
        OrganizationEnrollmentApprovalMode.RequiresApproval,
        OrganizationEnrollmentLinkStatus.Active,
        1,
        Now,
        Now);

    private sealed class StubDispatcher(WorkspaceStaffAccessPlanDto plan, List<string> order)
        : IRequestDispatcher
    {
        public WorkspaceStaffAccessPlanDto Plan { get; private set; } = plan;
        public bool Activated { get; private set; }
        public bool PreparedBeforeOrganizationCall { get; private set; }

        public Task<Result<TResponse>> SendAsync<TResponse>(
            ICommand<TResponse> command,
            CancellationToken cancellationToken = default)
        {
            if (command is PrepareWorkspaceStaffAccessPlanCommand)
            {
                order.Add("prepare");
                this.PreparedBeforeOrganizationCall = true;
                return Success<TResponse>(this.Plan);
            }

            if (command is ActivateWorkspaceStaffAccessPlanCommand)
            {
                order.Add("activate");
                this.Activated = true;
                this.Plan = this.Plan with
                {
                    Status = WorkspaceStaffAccessPlanStatus.Active,
                    Version = Math.Max(2, this.Plan.Version)
                };
                return Success<TResponse>(this.Plan);
            }

            throw new NotSupportedException(command.GetType().FullName);
        }

        public Task<Result<TResponse>> QueryAsync<TResponse>(
            IQuery<TResponse> query,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        private static Task<Result<TResponse>> Success<TResponse>(object value) =>
            Task.FromResult(Result.Success((TResponse)value));
    }

    private sealed class StubOrganizationIssuer(List<string> order) : IOrganizationJoinSourceIssuer
    {
        public OrganizationJoinSourceIssuance<OrganizationInvitationDto> InvitationResult { get; init; } =
            new(null, OrganizationJoinSourceIssuanceOutcome.Unknown, null, "not-configured");

        public OrganizationJoinSourceIssuance<OrganizationEnrollmentLinkDto> EnrollmentResult { get; init; } =
            new(null, OrganizationJoinSourceIssuanceOutcome.Unknown, null, "not-configured");

        public Task<OrganizationJoinSourceIssuance<OrganizationInvitationDto>> IssueInvitationAsync(
            OrganizationInvitationIssuanceRequest request,
            CancellationToken cancellationToken = default)
        {
            order.Add("organizations.invitation");
            return Task.FromResult(this.InvitationResult);
        }

        public Task<OrganizationJoinSourceIssuance<OrganizationEnrollmentLinkDto>> IssueEnrollmentLinkAsync(
            OrganizationEnrollmentLinkIssuanceRequest request,
            CancellationToken cancellationToken = default)
        {
            order.Add("organizations.enrollment");
            return Task.FromResult(this.EnrollmentResult);
        }
    }

    private sealed class StubScopeContext(string scopeId) : IScopeContext
    {
        public bool IsEnabled => true;
        public string? ScopeId => scopeId;
    }
}

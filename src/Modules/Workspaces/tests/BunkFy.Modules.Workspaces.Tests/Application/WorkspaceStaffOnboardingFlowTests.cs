namespace BunkFy.Modules.Workspaces.Tests;

using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Workspaces.Application;
using BunkFy.Modules.Workspaces.Application.Commands;
using BunkFy.Modules.Workspaces.Application.Ports;
using BunkFy.Modules.Workspaces.Contracts;
using BunkFy.Modules.Workspaces.Domain;
using Gma.Framework.AccessControl;
using Gma.Framework.Cqrs;
using Gma.Framework.Pagination;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;
using Gma.Modules.AccessControl.Contracts;
using Gma.Modules.Auth.Contracts;
using Gma.Modules.Organizations.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

[Trait("Category", "Unit")]
public sealed class WorkspaceStaffOnboardingFlowTests
{
    [Fact]
    public async Task Staff_failure_never_grants_workspace_access()
    {
        WorkspaceStaffOnboarding application = WorkspaceStaffOnboardingTests.CreateApplication();
        FakeRepository applications = new(application);
        FakeStaffProvisioner staff = new() { ErrorCode = "Staff.EmployeeNumberConflict" };
        FakeAccessControl access = new();
        using ServiceProvider provider = CreateProvider(applications, staff, access);
        ICommandHandler<RetryWorkspaceStaffOnboardingCommand, WorkspaceStaffOnboardingDto> handler =
            provider.GetRequiredService<ICommandHandler<RetryWorkspaceStaffOnboardingCommand, WorkspaceStaffOnboardingDto>>();

        Result<WorkspaceStaffOnboardingDto> result = await handler.HandleAsync(
            new RetryWorkspaceStaffOnboardingCommand(application.Id),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(WorkspaceStaffOnboardingState.Failed, application.Status);
        Assert.Null(application.StaffMemberId);
        Assert.Equal(1, staff.CallCount);
        Assert.Equal(0, access.AssignmentCallCount);
    }

    [Fact]
    public async Task Access_failure_retries_without_duplicate_staff_and_then_redacts_application()
    {
        WorkspaceStaffOnboarding application = WorkspaceStaffOnboardingTests.CreateApplication();
        FakeRepository applications = new(application);
        Guid staffMemberId = Guid.NewGuid();
        FakeStaffProvisioner staff = new() { StaffMemberId = staffMemberId };
        FakeAccessControl access = new() { FailAssignments = true };
        using ServiceProvider provider = CreateProvider(applications, staff, access);
        ICommandHandler<RetryWorkspaceStaffOnboardingCommand, WorkspaceStaffOnboardingDto> handler =
            provider.GetRequiredService<ICommandHandler<RetryWorkspaceStaffOnboardingCommand, WorkspaceStaffOnboardingDto>>();

        Result<WorkspaceStaffOnboardingDto> first = await handler.HandleAsync(
            new RetryWorkspaceStaffOnboardingCommand(application.Id),
            CancellationToken.None);
        access.FailAssignments = false;
        Result<WorkspaceStaffOnboardingDto> retried = await handler.HandleAsync(
            new RetryWorkspaceStaffOnboardingCommand(application.Id),
            CancellationToken.None);

        Assert.True(first.IsFailure);
        Assert.True(retried.IsSuccess, retried.Error.Code);
        Assert.Equal(1, staff.CallCount);
        Assert.Equal(2, access.AssignmentCallCount);
        Assert.Equal(WorkspaceStaffOnboardingStatus.Completed, retried.Value.Status);
        Assert.Equal(staffMemberId, retried.Value.StaffMemberId);
        Assert.Null(retried.Value.VerifiedAccountEmail);
        Assert.Null(retried.Value.DisplayName);
    }

    [Fact]
    public async Task Submission_derives_workspace_authority_from_the_token_and_verified_identity()
    {
        Guid sourceId = Guid.NewGuid();
        FakeRepository applications = new();
        FakeJoinTokenInspector tokens = new(
            WorkspaceStaffOnboardingTests.OrganizationId,
            sourceId);
        using ServiceProvider provider = CreateProvider(
            applications,
            new FakeStaffProvisioner(),
            new FakeAccessControl(),
            tokens);
        ICommandHandler<SubmitWorkspaceStaffOnboardingCommand, WorkspaceStaffOnboardingDto> handler =
            provider.GetRequiredService<ICommandHandler<SubmitWorkspaceStaffOnboardingCommand, WorkspaceStaffOnboardingDto>>();

        Result<WorkspaceStaffOnboardingDto> result = await handler.HandleAsync(
            new SubmitWorkspaceStaffOnboardingCommand(
                WorkspaceStaffOnboardingSourceKind.EnrollmentLink,
                "secret-token",
                WorkspaceStaffOnboardingTests.SubjectId,
                "Ada Operator",
                null,
                null,
                null,
                null,
                null,
                null),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error.Code);
        Assert.Equal(WorkspaceStaffOnboardingTests.OrganizationId, result.Value.OrganizationId);
        Assert.Equal(sourceId, result.Value.SourceId);
        Assert.Equal("verified@example.test", result.Value.VerifiedAccountEmail);
        Assert.Equal(WorkspaceStaffOnboardingTests.OrganizationId.ToString("D"),
            provider.GetRequiredService<IScopeContextAccessor>().ScopeId);
    }

    [Fact]
    public async Task Admission_requires_the_exact_source_subject_and_current_verified_email()
    {
        WorkspaceStaffOnboarding application = WorkspaceStaffOnboardingTests.CreateApplication();
        FakeContactReader contacts = new();
        using ServiceProvider provider = CreateProvider(
            new FakeRepository(application),
            new FakeStaffProvisioner(),
            new FakeAccessControl(),
            contacts: contacts);
        IOrganizationJoinAdmissionPolicy policy = provider
            .GetServices<IOrganizationJoinAdmissionPolicy>()
            .Single();

        OrganizationJoinAdmissionContext valid = new(
            OrganizationJoinAdmissionOperation.ClaimEnrollment,
            WorkspaceStaffOnboardingTests.OrganizationId,
            application.SourceId,
            null,
            application.SubjectId,
            application.SubjectId,
            OrganizationEnrollmentApprovalMode.RequiresApproval);

        Assert.True(await policy.IsAllowedAsync(valid));
        Assert.False(await policy.IsAllowedAsync(valid with { SourceId = Guid.NewGuid() }));
        Assert.False(await policy.IsAllowedAsync(valid with { ApplicantSubjectId = Guid.NewGuid().ToString("D") }));

        contacts.VerifiedEmail = "changed@example.test";
        Assert.False(await policy.IsAllowedAsync(valid));
    }

    [Fact]
    public async Task Approval_requires_the_claim_bound_to_the_staff_application()
    {
        WorkspaceStaffOnboarding application = WorkspaceStaffOnboardingTests.CreateApplication();
        Guid claimId = Guid.NewGuid();
        Assert.True(application.BindClaim(claimId, 1, WorkspaceStaffOnboardingTests.Now.AddMinutes(1)).IsSuccess);
        using ServiceProvider provider = CreateProvider(
            new FakeRepository(application),
            new FakeStaffProvisioner(),
            new FakeAccessControl());
        IOrganizationJoinAdmissionPolicy policy = provider
            .GetServices<IOrganizationJoinAdmissionPolicy>()
            .Single();

        OrganizationJoinAdmissionContext approval = new(
            OrganizationJoinAdmissionOperation.ApproveEnrollment,
            WorkspaceStaffOnboardingTests.OrganizationId,
            application.SourceId,
            claimId,
            application.SubjectId,
            Guid.NewGuid().ToString("D"),
            OrganizationEnrollmentApprovalMode.RequiresApproval);

        Assert.True(await policy.IsAllowedAsync(approval));
        Assert.False(await policy.IsAllowedAsync(approval with { ClaimId = Guid.NewGuid() }));
        Assert.False(await policy.IsAllowedAsync(approval with { ClaimId = null }));
    }

    private static ServiceProvider CreateProvider(
        FakeRepository applications,
        FakeStaffProvisioner staff,
        FakeAccessControl access,
        FakeJoinTokenInspector? tokens = null,
        FakeContactReader? contacts = null)
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton<IWorkspaceStaffOnboardingRepository>(applications);
        services.AddSingleton<IStaffOnboardingProvisioner>(staff);
        services.AddSingleton<IAccessControlRoleProvisioner>(access);
        services.AddSingleton<IOrganizationJoinTokenInspector>(tokens ?? new FakeJoinTokenInspector(
            WorkspaceStaffOnboardingTests.OrganizationId,
            Guid.NewGuid()));
        services.AddSingleton<IAuthMemberContactReader>(contacts ?? new FakeContactReader());
        services.AddSingleton<IScopeContextAccessor>(new FakeScopeContext());
        services.AddSingleton<IScopeContext>(provider => provider.GetRequiredService<IScopeContextAccessor>());
        services.AddSingleton<ISystemClock>(new FakeClock());
        services.AddSingleton<IIdGenerator>(new FakeIdGenerator());
        services.AddWorkspacesApplication("global");
        return services.BuildServiceProvider();
    }

    private sealed class FakeRepository(params WorkspaceStaffOnboarding[] seed)
        : IWorkspaceStaffOnboardingRepository
    {
        private readonly List<WorkspaceStaffOnboarding> applications = [.. seed];

        public Task<WorkspaceStaffOnboarding?> GetAsync(Guid applicationId, CancellationToken cancellationToken) =>
            Task.FromResult(this.applications.SingleOrDefault(item => item.Id == applicationId));

        public Task<WorkspaceStaffOnboarding?> GetBySourceAndSubjectAsync(
            WorkspaceStaffOnboardingSource sourceKind,
            Guid sourceId,
            string subjectId,
            CancellationToken cancellationToken) => Task.FromResult(this.applications.SingleOrDefault(item =>
                item.SourceKind == sourceKind &&
                item.SourceId == sourceId &&
                string.Equals(item.SubjectId, subjectId, StringComparison.Ordinal)));

        public Task<WorkspaceStaffOnboarding?> GetByClaimAsync(Guid claimId, CancellationToken cancellationToken) =>
            Task.FromResult(this.applications.SingleOrDefault(item => item.ClaimId == claimId));

        public Task<IReadOnlyList<WorkspaceStaffOnboarding>> ListActiveBySourceAsync(
            WorkspaceStaffOnboardingSource sourceKind,
            Guid sourceId,
            CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<WorkspaceStaffOnboarding>>(
                this.applications.Where(item => item.SourceKind == sourceKind && item.SourceId == sourceId).ToArray());

        public Task<WorkspaceStaffOnboardingListResponse> ListActionableAsync(
            PageRequest page,
            CancellationToken cancellationToken) => Task.FromResult(
                new WorkspaceStaffOnboardingListResponse([], page.Page, page.PageSize));

        public Task AddAsync(WorkspaceStaffOnboarding application, CancellationToken cancellationToken)
        {
            this.applications.Add(application);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeStaffProvisioner : IStaffOnboardingProvisioner
    {
        public Guid? StaffMemberId { get; init; } = Guid.NewGuid();
        public string? ErrorCode { get; init; }
        public int CallCount { get; private set; }

        public Task<StaffOnboardingProvisioningResult> ProvisionAsync(
            StaffOnboardingProvisioningRequest request,
            CancellationToken cancellationToken = default)
        {
            this.CallCount++;
            return Task.FromResult(this.ErrorCode is null
                ? new StaffOnboardingProvisioningResult(true, this.StaffMemberId, null)
                : new StaffOnboardingProvisioningResult(false, null, this.ErrorCode));
        }
    }

    private sealed class FakeAccessControl : IAccessControlRoleProvisioner
    {
        public bool FailAssignments { get; set; }
        public int AssignmentCallCount { get; private set; }

        public Task EnsureRoleAsync(AccessControlRoleDefinition role, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task EnsureAssignmentAsync(
            AccessSubject subject,
            string roleName,
            AccessScope scope,
            CancellationToken cancellationToken = default)
        {
            this.AssignmentCallCount++;
            return this.FailAssignments
                ? Task.FromException(new InvalidOperationException("Access unavailable."))
                : Task.CompletedTask;
        }

        public Task<AccessControlAssignmentRemovalOutcome> RemoveAssignmentAsync(
            AccessSubject subject,
            string roleName,
            AccessScope scope,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(AccessControlAssignmentRemovalOutcome.NotFound);

        public Task<bool> HasAssignmentAsync(
            AccessSubject subject,
            string roleName,
            AccessScope scope,
            CancellationToken cancellationToken = default) => Task.FromResult(false);

        public Task<AccessControlPage<AccessControlRoleAssignment>> ListAssignmentsAsync(
            string roleName,
            AccessScope scope,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default) => Task.FromResult(
                new AccessControlPage<AccessControlRoleAssignment>([], page, pageSize, false));
    }

    private sealed class FakeJoinTokenInspector(Guid organizationId, Guid sourceId)
        : IOrganizationJoinTokenInspector
    {
        public Task<OrganizationJoinTokenInspection<OrganizationInvitationPreviewDto>> InspectInvitationAsync(
            string token,
            CancellationToken cancellationToken = default) => Task.FromResult(
                new OrganizationJoinTokenInspection<OrganizationInvitationPreviewDto>(
                    new OrganizationInvitationPreviewDto(
                        sourceId,
                        organizationId,
                        "Workspace",
                        "workspace",
                        false,
                        WorkspaceStaffOnboardingTests.Now.AddDays(1),
                        OrganizationInvitationStatus.Pending),
                    null));

        public Task<OrganizationJoinTokenInspection<OrganizationEnrollmentPreviewDto>> InspectEnrollmentAsync(
            string token,
            CancellationToken cancellationToken = default) => Task.FromResult(
                new OrganizationJoinTokenInspection<OrganizationEnrollmentPreviewDto>(
                    new OrganizationEnrollmentPreviewDto(
                        sourceId,
                        organizationId,
                        "Workspace",
                        "workspace",
                        WorkspaceStaffOnboardingTests.Now.AddDays(1),
                        5,
                        OrganizationEnrollmentApprovalMode.RequiresApproval,
                        OrganizationEnrollmentLinkStatus.Active),
                    null));
    }

    private sealed class FakeContactReader : IAuthMemberContactReader
    {
        public string? VerifiedEmail { get; set; } = "verified@example.test";

        public ValueTask<string?> GetPreferredVerifiedEmailAsync(
            string scopeId,
            Guid memberId,
            CancellationToken cancellationToken = default) => ValueTask.FromResult(this.VerifiedEmail);
    }

    private sealed class FakeScopeContext : IScopeContextAccessor
    {
        public bool IsEnabled => !string.IsNullOrWhiteSpace(this.ScopeId);
        public string? ScopeId { get; private set; }
        public void SetScope(string scopeId) => this.ScopeId = scopeId;
        public void ClearScope() => this.ScopeId = null;
    }

    private sealed class FakeClock : ISystemClock
    {
        private int ticks;
        public DateTimeOffset UtcNow => WorkspaceStaffOnboardingTests.Now.AddSeconds(this.ticks++);
    }

    private sealed class FakeIdGenerator : IIdGenerator
    {
        public Guid NewId() => Guid.CreateVersion7();
    }
}

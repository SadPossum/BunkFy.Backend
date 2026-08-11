namespace BunkFy.Modules.Workspaces.Tests;

using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Workspaces.Application;
using BunkFy.Modules.Workspaces.Application.Handlers;
using BunkFy.Modules.Workspaces.Application.Ports;
using BunkFy.Modules.Workspaces.Application.Queries;
using BunkFy.Modules.Workspaces.Contracts;
using BunkFy.Modules.Workspaces.Domain;
using Gma.Framework.Pagination;
using Gma.Framework.Results;
using Gma.Framework.Scoping;
using Xunit;

[Trait("Category", "Unit")]
public sealed class GetOwnWorkspaceStaffOnboardingProfileAuthorityTests
{
    private const string TenantId =
        "71000000-0000-0000-0000-000000000001";
    private const string SubjectId = "subject:profile-reader";
    private static readonly Guid SourceId =
        Guid.Parse("71000000-0000-0000-0000-000000000002");
    private static readonly DateTimeOffset Now =
        new(2026, 8, 11, 14, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Exact_absent_application_is_disclosed_after_all_locks()
    {
        WorkspaceStaffOnboarding application = CreateApplication();
        List<string> calls = [];
        GetOwnWorkspaceStaffOnboardingQueryHandler handler = CreateHandler(
            application,
            new RecordingOperationLock(calls),
            new MutableOutcomeReader(calls),
            new InlineReadBoundary(calls));

        Result<WorkspaceStaffOnboardingDto> result = await handler.HandleAsync(
            Query(),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error.Code);
        Assert.Equal("Applicant Profile", result.Value.DisplayName);
        Assert.Equal(
            ["tenant", "source", "applicant", "application", "staff"],
            calls);
    }

    [Fact]
    public async Task Staff_commit_while_waiting_for_application_lock_cannot_leak_staging()
    {
        WorkspaceStaffOnboarding application = CreateApplication();
        List<string> calls = [];
        CoordinatedOperationLock operationLock = new(calls);
        MutableOutcomeReader outcomes = new(calls);
        GetOwnWorkspaceStaffOnboardingQueryHandler handler = CreateHandler(
            application,
            operationLock,
            outcomes,
            new InlineReadBoundary(calls));

        Task<Result<WorkspaceStaffOnboardingDto>> read = handler.HandleAsync(
            Query(),
            CancellationToken.None);
        await operationLock.ApplicationLockRequested.Task.WaitAsync(
            TimeSpan.FromSeconds(5));
        Assert.Equal(0, outcomes.ReadCount);

        outcomes.Outcome = new StaffWorkspaceOnboardingIdentityAnchorOutcome(
            application.Id,
            StaffWorkspaceOnboardingIdentityAnchorOutcomeStatus.Unresolved,
            Guid.Parse("71000000-0000-0000-0000-000000000003"),
            StaffWorkspaceOnboardingIdentityAnchorTargetLifecycle.Active,
            StaffWorkspaceOnboardingIdentityAnchorSubjectMatch.Exact,
            WorkspaceApplicationVersion: null,
            ResolutionDisposition: null,
            Guid.Parse("71000000-0000-0000-0000-000000000004"));
        operationLock.ReleaseApplicationLock.TrySetResult();

        Result<WorkspaceStaffOnboardingDto> result = await read.WaitAsync(
            TimeSpan.FromSeconds(5));

        Assert.Equal(
            WorkspaceStaffOnboardingApplicationErrors
                .ProfileMutationAuthorityUnavailable,
            result.Error);
        Assert.Equal(1, outcomes.ReadCount);
        Assert.Equal("Applicant Profile", application.DisplayName);
    }

    [Fact]
    public async Task Read_boundary_failure_does_not_touch_or_disclose_application()
    {
        WorkspaceStaffOnboarding application = CreateApplication();
        MutableOutcomeReader outcomes = new([]);
        GetOwnWorkspaceStaffOnboardingQueryHandler handler = CreateHandler(
            application,
            new RecordingOperationLock([]),
            outcomes,
            new FailingReadBoundary());

        Result<WorkspaceStaffOnboardingDto> result = await handler.HandleAsync(
            Query(),
            CancellationToken.None);

        Assert.Equal(
            WorkspaceOperationalAdmissionErrors.AdmissionUnavailable,
            result.Error);
        Assert.Equal(0, outcomes.ReadCount);
        Assert.Equal("Applicant Profile", application.DisplayName);
    }

    private static GetOwnWorkspaceStaffOnboardingQueryHandler CreateHandler(
        WorkspaceStaffOnboarding application,
        IWorkspaceStaffOnboardingOperationLock operationLock,
        IStaffWorkspaceOnboardingIdentityAnchorOutcomeReader outcomes,
        IWorkspaceStaffOnboardingSerializedReadBoundary boundary)
    {
        ApplicationRepository applications = new(application);
        return new(
            WorkspaceStaffOnboardingMutationTestSupport.Create(
                applications,
                operationLock),
            boundary,
            outcomes,
            WorkspaceOperationalAdmissionTestSupport.Allowed(TenantId),
            new TestScopeContext());
    }

    private static GetOwnWorkspaceStaffOnboardingQuery Query() =>
        new(
            WorkspaceStaffOnboardingSourceKind.EnrollmentLink,
            SourceId,
            SubjectId);

    private static WorkspaceStaffOnboarding CreateApplication() =>
        WorkspaceStaffOnboarding.Create(
            Guid.Parse("71000000-0000-0000-0000-000000000005"),
            TenantId,
            WorkspaceStaffOnboardingSource.EnrollmentLink,
            SourceId,
            SubjectId,
            "verified@example.test",
            "Applicant Profile",
            "Applicant Legal",
            "work@example.test",
            "+1 555 0100",
            "EMP-710",
            "Manager",
            "Operations",
            Now).Value;

    private sealed class InlineReadBoundary(List<string> calls)
        : IWorkspaceStaffOnboardingSerializedReadBoundary
    {
        public Task<Result<T>> RunAsync<T>(
            Func<CancellationToken, Task<Result<T>>> read,
            CancellationToken cancellationToken)
        {
            calls.Add("tenant");
            return read(cancellationToken);
        }
    }

    private sealed class FailingReadBoundary
        : IWorkspaceStaffOnboardingSerializedReadBoundary
    {
        public Task<Result<T>> RunAsync<T>(
            Func<CancellationToken, Task<Result<T>>> read,
            CancellationToken cancellationToken) =>
            Task.FromResult(Result.Failure<T>(
                WorkspaceOperationalAdmissionErrors.AdmissionUnavailable));
    }

    private class RecordingOperationLock(List<string> calls)
        : IWorkspaceStaffOnboardingOperationLock
    {
        protected List<string> Calls { get; } = calls;

        public virtual Task AcquireSourceReadAsync(
            Guid sourceId,
            CancellationToken cancellationToken)
        {
            this.Calls.Add("source");
            return Task.CompletedTask;
        }

        public Task AcquireSourceWriteAsync(
            Guid sourceId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public virtual Task AcquireApplicantAsync(
            Guid sourceId,
            string subjectId,
            CancellationToken cancellationToken)
        {
            this.Calls.Add("applicant");
            return Task.CompletedTask;
        }

        public virtual Task<bool> TryAcquireAsync(
            Guid applicationId,
            CancellationToken cancellationToken)
        {
            this.Calls.Add("application");
            return Task.FromResult(true);
        }
    }

    private sealed class CoordinatedOperationLock(List<string> calls)
        : RecordingOperationLock(calls)
    {
        public TaskCompletionSource ApplicationLockRequested { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseApplicationLock { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async Task<bool> TryAcquireAsync(
            Guid applicationId,
            CancellationToken cancellationToken)
        {
            this.Calls.Add("application-wait");
            this.ApplicationLockRequested.TrySetResult();
            await this.ReleaseApplicationLock.Task.WaitAsync(cancellationToken);
            this.Calls.Add("application");
            return true;
        }
    }

    private sealed class MutableOutcomeReader(List<string> calls)
        : IStaffWorkspaceOnboardingIdentityAnchorOutcomeReader
    {
        public StaffWorkspaceOnboardingIdentityAnchorOutcome? Outcome
        {
            get;
            set;
        }

        public int ReadCount { get; private set; }

        public Task<IReadOnlyList<
            StaffWorkspaceOnboardingIdentityAnchorOutcome>> ReadAsync(
            IReadOnlyList<StaffWorkspaceOnboardingIdentityAnchorOutcomeRequest>
                requests,
            CancellationToken cancellationToken = default)
        {
            this.ReadCount++;
            calls.Add("staff");
            StaffWorkspaceOnboardingIdentityAnchorOutcomeRequest request =
                Assert.Single(requests);
            return Task.FromResult<IReadOnlyList<
                StaffWorkspaceOnboardingIdentityAnchorOutcome>>([
                    this.Outcome ??
                    StubStaffWorkspaceOnboardingIdentityAnchorOutcomeReader
                        .Absent(request)
                ]);
        }
    }

    private sealed class ApplicationRepository(
        WorkspaceStaffOnboarding application)
        : IWorkspaceStaffOnboardingRepository
    {
        public Task<Guid?> FindIdBySourceAndSubjectAsync(
            WorkspaceStaffOnboardingSource sourceKind,
            Guid sourceId,
            string subjectId,
            CancellationToken cancellationToken) =>
            Task.FromResult<Guid?>(
                application.SourceKind == sourceKind &&
                application.SourceId == sourceId &&
                string.Equals(
                    application.SubjectId,
                    subjectId,
                    StringComparison.Ordinal)
                    ? application.Id
                    : null);

        public Task<WorkspaceStaffOnboarding?> GetOperationalAsync(
            Guid applicationId,
            CancellationToken cancellationToken) =>
            Task.FromResult<WorkspaceStaffOnboarding?>(
                application.Id == applicationId ? application : null);

        public Task<WorkspaceStaffOnboardingCoordinate?> FindCoordinateAsync(
            Guid applicationId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<WorkspaceStaffOnboarding?> GetAsync(
            Guid applicationId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<WorkspaceStaffOnboarding?>
            GetOperationalBySourceAndSubjectAsync(
                WorkspaceStaffOnboardingSource sourceKind,
                Guid sourceId,
                string subjectId,
                CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<WorkspaceStaffOnboarding?> GetByClaimAsync(
            Guid claimId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<WorkspaceStaffOnboarding>>
            ListActiveBySourceAsync(
                WorkspaceStaffOnboardingSource sourceKind,
                Guid sourceId,
                CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<WorkspaceStaffOnboardingListResponse> ListActionableAsync(
            PageRequest page,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task ReloadAsync(
            WorkspaceStaffOnboarding candidate,
            CancellationToken cancellationToken) => Task.CompletedTask;

        public Task AddAsync(
            WorkspaceStaffOnboarding candidate,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class TestScopeContext : IScopeContext
    {
        public bool IsEnabled => true;
        public string? ScopeId => TenantId;
    }
}

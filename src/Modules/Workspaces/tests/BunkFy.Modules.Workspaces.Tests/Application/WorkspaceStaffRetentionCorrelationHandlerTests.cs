namespace BunkFy.Modules.Workspaces.Tests;

using BunkFy.Modules.Workspaces.Application.Commands;
using BunkFy.Modules.Workspaces.Application.Handlers;
using BunkFy.Modules.Workspaces.Application.Ports;
using BunkFy.Modules.Workspaces.Domain;
using Gma.Framework.Results;
using Gma.Framework.Scoping;
using Xunit;

[Trait("Category", "Unit")]
public sealed class WorkspaceStaffRetentionCorrelationHandlerTests
{
    private static readonly Guid ReceiptId =
        Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid ExecutionId =
        Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid StaffMemberId =
        Guid.Parse("30000000-0000-0000-0000-000000000001");
    private const string TenantId =
        "40000000-0000-0000-0000-000000000001";

    [Fact]
    public async Task Valid_request_is_normalized_before_persistence()
    {
        FakeRepository repository = new();
        ScrubWorkspaceStaffRetentionCorrelationCommandHandler handler =
            new(repository, new TestScopeContext(TenantId));
        DateTimeOffset completedAt =
            new DateTimeOffset(
                2026,
                7,
                30,
                12,
                0,
                0,
                TimeSpan.FromHours(3))
                .AddTicks(7);

        Result<WorkspaceStaffRetentionCorrelationReceipt> result =
            await handler.HandleAsync(
                new ScrubWorkspaceStaffRetentionCorrelationCommand(
                    ReceiptId,
                    ExecutionId,
                    $" {TenantId} ",
                    StaffMemberId,
                    SelectedStaffVersion: 7,
                    SubjectId: " subject-a ",
                    completedAt),
                CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error.Code);
        Assert.NotNull(repository.Request);
        Assert.Equal(TenantId, repository.Request.TenantId);
        Assert.Equal("subject-a", repository.Request.SubjectId);
        Assert.Equal(TimeSpan.Zero, repository.Request.CompletedAtUtc.Offset);
        Assert.Equal(0, repository.Request.CompletedAtUtc.Ticks % 10);
    }

    [Fact]
    public async Task Cross_scope_request_fails_before_persistence()
    {
        FakeRepository repository = new();
        ScrubWorkspaceStaffRetentionCorrelationCommandHandler handler =
            new(
                repository,
                new TestScopeContext(
                    "50000000-0000-0000-0000-000000000001"));

        Result<WorkspaceStaffRetentionCorrelationReceipt> result =
            await handler.HandleAsync(
                new ScrubWorkspaceStaffRetentionCorrelationCommand(
                    ReceiptId,
                    ExecutionId,
                    TenantId,
                    StaffMemberId,
                    SelectedStaffVersion: 7,
                    SubjectId: "subject-a",
                    DateTimeOffset.UtcNow),
                CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(
            WorkspaceStaffRetentionErrors.RequestInvalid,
            result.Error);
        Assert.Null(repository.Request);
    }

    [Fact]
    public async Task Malformed_request_fails_before_persistence()
    {
        FakeRepository repository = new();
        ScrubWorkspaceStaffRetentionCorrelationCommandHandler handler =
            new(repository, new TestScopeContext(TenantId));

        Result<WorkspaceStaffRetentionCorrelationReceipt> result =
            await handler.HandleAsync(
                new ScrubWorkspaceStaffRetentionCorrelationCommand(
                    Guid.Empty,
                    ExecutionId,
                    TenantId,
                    StaffMemberId,
                    SelectedStaffVersion: 0,
                    SubjectId: new string(
                        'x',
                        WorkspaceStaffAccessProcess.SubjectIdMaxLength +
                        1),
                    CompletedAtUtc: default),
                CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(
            WorkspaceStaffRetentionErrors.RequestInvalid,
            result.Error);
        Assert.Null(repository.Request);
    }

    private sealed class FakeRepository
        : IWorkspaceStaffRetentionCorrelationRepository
    {
        public WorkspaceStaffRetentionCorrelationScrubRequest? Request
        {
            get;
            private set;
        }

        public Task<WorkspaceStaffRetentionCorrelationReceipt?> GetAsync(
            Guid staffMemberId,
            long selectedStaffVersion,
            CancellationToken cancellationToken) =>
            Task.FromResult<
                WorkspaceStaffRetentionCorrelationReceipt?>(null);

        public Task<Result<WorkspaceStaffRetentionCorrelationReceipt>>
            ScrubAsync(
                WorkspaceStaffRetentionCorrelationScrubRequest request,
                CancellationToken cancellationToken)
        {
            this.Request = request;
            return Task.FromResult(
                WorkspaceStaffRetentionCorrelationReceipt.Create(
                    request.ReceiptId,
                    request.TenantId,
                    request.ExecutionId,
                    request.StaffMemberId,
                    request.SelectedStaffVersion,
                    onboardingRecordsScrubbed: 0,
                    accessProcessRecordsScrubbed: 0,
                    accessPlanRecordsScrubbed: 0,
                    request.CompletedAtUtc));
        }
    }

    private sealed class TestScopeContext(string scopeId)
        : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => scopeId;
    }
}

namespace BunkFy.Extensions.DataRights.TaskRuntime.Tests;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Workspaces.Contracts;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;
using Gma.Modules.TaskRuntime.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

[Trait("Category", "Unit")]
public sealed class TaskRuntimeTenantTerminationContributorTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 4, 8, 30, 0, TimeSpan.Zero);
    private static readonly Guid WorkspaceId =
        Guid.Parse("08b74d32-84da-4b44-80cc-b74dc8d5bacc");
    private static readonly string TenantId = WorkspaceId.ToString("D");
    private static readonly Guid ProcessId =
        Guid.Parse("74875bfc-05bb-474a-a182-12552252108a");
    private static readonly Guid TerminationEpoch =
        Guid.Parse("c8d15486-3678-47ee-9365-c74a27f3bea9");

    [Fact]
    public void Descriptor_is_mandatory_destroy_only_after_access_control()
    {
        TaskRuntimeTenantTerminationContributor contributor =
            CreateContributor(new TestLifecycle());

        Assert.Equal(
            TaskRuntimeTenantTerminationMetadata.OwnerKey,
            contributor.Descriptor.OwnerKey);
        Assert.Equal(
            [TenantTerminationContributionPhase.Destroy],
            contributor.Descriptor.PhasePlans.Select(plan => plan.Phase));
        Assert.Equal(
            TaskRuntimeTenantTerminationMetadata.DependencyOwnerKeys,
            Assert.Single(contributor.Descriptor.PhasePlans)
                .DependsOnOwnerKeys);
        Assert.True(contributor.Descriptor.MandatoryForProduction);
        Assert.Equal(
            TaskRuntimeTenantTerminationMetadata.CatalogSha256,
            contributor.Descriptor.CatalogSha256);
    }

    [Fact]
    public void Registration_is_idempotent_and_exposes_no_export_contributor()
    {
        ServiceCollection services = [];

        services.AddBunkFyTaskRuntimeDataRights();
        services.AddBunkFyTaskRuntimeDataRights();

        Assert.Single(services, descriptor =>
            descriptor.ServiceType == typeof(ITenantTerminationContributor) &&
            descriptor.ImplementationType ==
                typeof(TaskRuntimeTenantTerminationContributor));
        Assert.DoesNotContain(
            services,
            descriptor => descriptor.ServiceType ==
                typeof(ITenantTerminationExportContributor));
    }

    [Fact]
    public async Task Missing_scope_closes_at_exact_revision_zero()
    {
        TenantTerminationContributionRequest request = Request();
        TestLifecycle lifecycle = new()
        {
            Destroy = (destroy, _) =>
            {
                Assert.Equal(request.IdempotencyKey, destroy.OperationId);
                Assert.Equal(TenantId, destroy.ScopeId);
                Assert.Equal(0, destroy.ExpectedRevision);
                Assert.Equal(
                    TaskRuntimeScopeLifecycleLimits.MaximumDestroyBatchSize,
                    destroy.BatchSize);
                return Task.FromResult(new TaskRuntimeScopeDestroyResult(
                    TaskRuntimeScopeDestroyStatus.Completed,
                    null,
                    Receipt(request.IdempotencyKey, selectedRevision: 0)));
            }
        };

        TenantTerminationContributionResult result =
            await CreateContributor(lifecycle).ExecuteAsync(
                request,
                CancellationToken.None);

        Assert.Equal(TenantTerminationContributionStatus.Completed, result.Status);
        Assert.Equal(0, result.AffectedCount);
        Assert.Equal(0, result.SelectedProofRevision);
        Assert.Equal(1, result.ResultingProofRevision);
    }

    [Fact]
    public async Task Open_scope_reports_bounded_durable_progress()
    {
        TenantTerminationContributionRequest request = Request();
        TestLifecycle lifecycle = new()
        {
            Snapshot = (_, _) => Task.FromResult(OpenSnapshot()),
            Destroy = (destroy, _) => Task.FromResult(
                new TaskRuntimeScopeDestroyResult(
                    TaskRuntimeScopeDestroyStatus.InProgress,
                    Progress(
                        request.IdempotencyKey,
                        TaskRuntimeScopeDestructionStage.ControlMessages,
                        removedCount: 1000,
                        completedBatches: 1,
                        remainingActive: 0),
                    null))
        };

        TenantTerminationContributionResult result =
            await CreateContributor(lifecycle).ExecuteAsync(
                request,
                CancellationToken.None);

        Assert.Equal(
            TenantTerminationContributionStatus.RetryRequired,
            result.Status);
        Assert.Equal("task-runtime.termination.destroy-in-progress", result.ResultCode);
        Assert.Equal(1000, result.AffectedCount);
        Assert.Null(result.SelectedProofRevision);
        Assert.Null(result.ResultingProofRevision);
    }

    [Fact]
    public async Task Closing_scope_with_active_lease_is_retryable()
    {
        TenantTerminationContributionRequest request = Request();
        TestLifecycle lifecycle = new()
        {
            Snapshot = (_, _) => Task.FromResult(new TaskRuntimeScopeSnapshot(
                TaskRuntimeScopeStatus.Closing,
                Revision: 1,
                SelectedRevision: 0,
                TotalRunCount: 1,
                ActiveRunCount: 1,
                ControlMessageCount: 0)),
            Destroy = (_, _) => Task.FromResult(
                new TaskRuntimeScopeDestroyResult(
                    TaskRuntimeScopeDestroyStatus.Busy,
                    Progress(
                        request.IdempotencyKey,
                        TaskRuntimeScopeDestructionStage.QuiesceRuns,
                        removedCount: 0,
                        completedBatches: 0,
                        remainingActive: 1),
                    null))
        };

        TenantTerminationContributionResult result =
            await CreateContributor(lifecycle).ExecuteAsync(
                request,
                CancellationToken.None);

        Assert.Equal(
            TenantTerminationContributionStatus.RetryRequired,
            result.Status);
        Assert.Equal("task-runtime.termination.destroy-scope-busy", result.ResultCode);
        Assert.Equal(1, result.RemainingActiveCount);
    }

    [Fact]
    public async Task Closed_scope_replays_from_its_durable_selected_revision()
    {
        TenantTerminationContributionRequest request = Request();
        TestLifecycle lifecycle = new()
        {
            Snapshot = (_, _) => Task.FromResult(new TaskRuntimeScopeSnapshot(
                TaskRuntimeScopeStatus.Closed,
                Revision: 5,
                SelectedRevision: 4,
                TotalRunCount: 0,
                ActiveRunCount: 0,
                ControlMessageCount: 0)),
            Destroy = (destroy, _) =>
            {
                Assert.Equal(4, destroy.ExpectedRevision);
                return Task.FromResult(new TaskRuntimeScopeDestroyResult(
                    TaskRuntimeScopeDestroyStatus.Replayed,
                    null,
                    Receipt(
                        request.IdempotencyKey,
                        selectedRevision: 4,
                        removedCount: 3,
                        completedBatches: 2)));
            }
        };

        TenantTerminationContributionResult result =
            await CreateContributor(lifecycle).ExecuteAsync(
                request,
                CancellationToken.None);

        Assert.Equal(TenantTerminationContributionStatus.Completed, result.Status);
        Assert.Equal(3, result.AffectedCount);
        Assert.Equal(4, result.SelectedProofRevision);
        Assert.Equal(5, result.ResultingProofRevision);
    }

    [Theory]
    [InlineData(TaskRuntimeScopeDestroyStatus.Stale, true)]
    [InlineData(TaskRuntimeScopeDestroyStatus.Conflict, false)]
    public async Task Lifecycle_conflicts_are_mapped_without_terminal_proof(
        TaskRuntimeScopeDestroyStatus status,
        bool retryable)
    {
        TestLifecycle lifecycle = new()
        {
            Snapshot = (_, _) => Task.FromResult(OpenSnapshot()),
            Destroy = (_, _) => Task.FromResult(
                new TaskRuntimeScopeDestroyResult(status, null, null))
        };

        TenantTerminationContributionResult result =
            await CreateContributor(lifecycle).ExecuteAsync(
                Request(),
                CancellationToken.None);

        Assert.Equal(
            retryable
                ? TenantTerminationContributionStatus.RetryRequired
                : TenantTerminationContributionStatus.Failed,
            result.Status);
        Assert.Null(result.SelectedProofRevision);
        Assert.Null(result.ResultingProofRevision);
    }

    [Fact]
    public async Task Malformed_snapshot_fails_before_destruction()
    {
        bool destroyed = false;
        TestLifecycle lifecycle = new()
        {
            Snapshot = (_, _) => Task.FromResult(new TaskRuntimeScopeSnapshot(
                TaskRuntimeScopeStatus.Closing,
                Revision: 1,
                SelectedRevision: null,
                TotalRunCount: 1,
                ActiveRunCount: 0,
                ControlMessageCount: 0)),
            Destroy = (_, _) =>
            {
                destroyed = true;
                throw new InvalidOperationException();
            }
        };

        TenantTerminationContributionResult result =
            await CreateContributor(lifecycle).ExecuteAsync(
                Request(),
                CancellationToken.None);

        Assert.False(destroyed);
        Assert.Equal(TenantTerminationContributionStatus.Failed, result.Status);
        Assert.Equal(
            "task-runtime.termination.destroy-scope-invalid",
            result.ResultCode);
    }

    [Fact]
    public async Task Malformed_progress_and_receipt_fail_closed()
    {
        TenantTerminationContributionRequest request = Request();
        TestLifecycle badProgress = new()
        {
            Snapshot = (_, _) => Task.FromResult(OpenSnapshot()),
            Destroy = (_, _) => Task.FromResult(
                new TaskRuntimeScopeDestroyResult(
                    TaskRuntimeScopeDestroyStatus.InProgress,
                    Progress(
                        Guid.NewGuid(),
                        TaskRuntimeScopeDestructionStage.ControlMessages,
                        1,
                        1,
                        0),
                    null))
        };
        TestLifecycle badReceipt = new()
        {
            Snapshot = (_, _) => Task.FromResult(OpenSnapshot()),
            Destroy = (_, _) => Task.FromResult(
                new TaskRuntimeScopeDestroyResult(
                    TaskRuntimeScopeDestroyStatus.Completed,
                    null,
                    Receipt(Guid.NewGuid(), selectedRevision: 0)))
        };

        TenantTerminationContributionResult progressResult =
            await CreateContributor(badProgress).ExecuteAsync(
                request,
                CancellationToken.None);
        TenantTerminationContributionResult receiptResult =
            await CreateContributor(badReceipt).ExecuteAsync(
                request,
                CancellationToken.None);

        Assert.Equal(TenantTerminationContributionStatus.Failed, progressResult.Status);
        Assert.Equal(TenantTerminationContributionStatus.Failed, receiptResult.Status);
        Assert.Equal(
            "task-runtime.termination.destroy-response-invalid",
            progressResult.ResultCode);
        Assert.Equal(
            "task-runtime.termination.destroy-response-invalid",
            receiptResult.ResultCode);
    }

    [Fact]
    public async Task Fence_or_coordinate_mismatch_prevents_scope_selection()
    {
        bool selected = false;
        TestLifecycle lifecycle = new()
        {
            Snapshot = (_, _) =>
            {
                selected = true;
                throw new InvalidOperationException();
            }
        };
        TaskRuntimeTenantTerminationContributor noFence = new(
            lifecycle,
            new TestScopeContext(TenantId),
            new FixedClock());

        TenantTerminationContributionResult fenceResult =
            await noFence.ExecuteAsync(Request(), CancellationToken.None);
        TenantTerminationContributionResult coordinateResult =
            await CreateContributor(lifecycle, scopeId: Guid.NewGuid().ToString("D"))
                .ExecuteAsync(Request(), CancellationToken.None);

        Assert.False(selected);
        Assert.Equal(
            TenantTerminationContributionStatus.RetryRequired,
            fenceResult.Status);
        Assert.Equal(
            TenantTerminationContributionStatus.Failed,
            coordinateResult.Status);
    }

    [Fact]
    public async Task Export_phase_is_explicitly_unsupported()
    {
        TenantTerminationContributionResult result =
            await CreateContributor(new TestLifecycle()).ExecuteAsync(
                Request() with
                {
                    Phase = TenantTerminationContributionPhase.Export
                },
                CancellationToken.None);

        Assert.Equal(TenantTerminationContributionStatus.Failed, result.Status);
        Assert.Equal(
            "task-runtime.termination.export-not-supported",
            result.ResultCode);
    }

    private static TaskRuntimeTenantTerminationContributor CreateContributor(
        ITaskRuntimeScopeLifecycle lifecycle,
        WorkspaceTerminationFenceSnapshot? fence = null,
        string? scopeId = null) =>
        new(
            lifecycle,
            new TestScopeContext(scopeId ?? TenantId),
            new FixedClock(),
            new TestFenceReader(fence ?? Fence()));

    private static TenantTerminationContributionRequest Request() =>
        new(
            TenantTerminationContract.CurrentVersion,
            TenantId,
            ProcessId,
            Guid.Parse("0500738e-56a5-4d59-b64e-ed3452b225da"),
            ApprovalRevision: 2,
            OperationRevision: 4,
            TerminationEpoch,
            TenantTerminationContributionPhase.Destroy,
            Guid.Parse("e9ab9a84-973e-4ea9-b9e1-b3e256074769"),
            Guid.Parse("d07d7678-42c2-4d98-920d-4f2f91f67173"),
            new string('b', 64),
            "system:tenant-termination",
            Now.AddHours(1));

    private static TaskRuntimeScopeSnapshot OpenSnapshot() =>
        new(
            TaskRuntimeScopeStatus.Open,
            Revision: 0,
            SelectedRevision: null,
            TotalRunCount: 2,
            ActiveRunCount: 0,
            ControlMessageCount: 1);

    private static WorkspaceTerminationFenceSnapshot Fence() =>
        new(
            ProcessId,
            TerminationEpoch,
            WorkspaceTerminationFenceState.Frozen,
            Version: 5);

    private static TaskRuntimeScopeDestroyProgress Progress(
        Guid operationId,
        TaskRuntimeScopeDestructionStage stage,
        long removedCount,
        int completedBatches,
        long remainingActive) =>
        new(
            operationId,
            SelectedRevision: 0,
            ResultingRevision: 1,
            TaskRuntimeScopeLifecycleLimits.MaximumDestroyBatchSize,
            stage,
            removedCount,
            completedBatches,
            remainingActive,
            RemovalProofVersion: 1,
            new string('a', 64),
            Now.AddMinutes(-2),
            Now.AddMinutes(-1));

    private static TaskRuntimeScopeDestroyReceipt Receipt(
        Guid operationId,
        long selectedRevision,
        long removedCount = 0,
        int completedBatches = 0) =>
        new(
            operationId,
            selectedRevision,
            ResultingRevision: selectedRevision + 1,
            TaskRuntimeScopeLifecycleLimits.MaximumDestroyBatchSize,
            removedCount,
            completedBatches,
            RemovalProofVersion: 1,
            new string('a', 64),
            Now.AddMinutes(-2),
            Now.AddMinutes(-1));

    private sealed class TestScopeContext(string scopeId) : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => scopeId;
    }

    private sealed class FixedClock : ISystemClock
    {
        public DateTimeOffset UtcNow => Now;
    }

    private sealed class TestFenceReader(
        WorkspaceTerminationFenceSnapshot snapshot)
        : IWorkspaceTerminationFenceReader
    {
        public Task<WorkspaceTerminationFenceSnapshot?> GetCurrentAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<WorkspaceTerminationFenceSnapshot?>(snapshot);
    }

    private sealed record TestLifecycle : ITaskRuntimeScopeLifecycle
    {
        public Func<string, CancellationToken, Task<TaskRuntimeScopeSnapshot>>
            Snapshot
        { get; init; } = (_, _) => Task.FromResult(
            new TaskRuntimeScopeSnapshot(
                TaskRuntimeScopeStatus.Missing,
                Revision: 0,
                SelectedRevision: null,
                TotalRunCount: 0,
                ActiveRunCount: 0,
                ControlMessageCount: 0));

        public Func<
            TaskRuntimeScopeDestroyRequest,
            CancellationToken,
            Task<TaskRuntimeScopeDestroyResult>> Destroy
        { get; init; } = (_, _) => throw new InvalidOperationException();

        public Task<TaskRuntimeScopeSnapshot> GetSnapshotAsync(
            string scopeId,
            CancellationToken cancellationToken) =>
            this.Snapshot(scopeId, cancellationToken);

        public Task<TaskRuntimeScopeDestroyResult> DestroyBatchAsync(
            TaskRuntimeScopeDestroyRequest request,
            CancellationToken cancellationToken) =>
            this.Destroy(request, cancellationToken);
    }
}

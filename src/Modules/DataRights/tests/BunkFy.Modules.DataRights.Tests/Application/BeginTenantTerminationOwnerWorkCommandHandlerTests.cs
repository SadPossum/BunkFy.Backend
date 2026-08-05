namespace BunkFy.Modules.DataRights.Tests.Application;

using BunkFy.Modules.DataRights.Application;
using BunkFy.Modules.DataRights.Application.Commands;
using BunkFy.Modules.DataRights.Application.Handlers;
using BunkFy.Modules.DataRights.Application.Models;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Models;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;
using Xunit;

[Trait("Category", "Unit")]
public sealed class BeginTenantTerminationOwnerWorkCommandHandlerTests
{
    private static readonly string Digest = new('a', 64);
    private static readonly DateTimeOffset Now =
        new(2026, 8, 4, 17, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Ready_owner_starts_with_exact_durable_coordinates()
    {
        Fixture fixture = CreateFixture(
            [Stub("workspaces"), Stub("reservations", ["workspaces"])]);
        TenantTerminationPlannedDispatch dispatch = Assert.Single(
            fixture.Planner.FindReadyDispatches(
                fixture.Process,
                fixture.Repository.WorkItems).Value);

        Result<TenantTerminationOwnerWorkStart> result =
            await fixture.Handler.HandleAsync(
                Command(fixture.Process, dispatch),
                CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.DispatchRequired);
        Assert.Equal(
            TenantTerminationOwnerWorkState.Processing,
            result.Value.State);
        Assert.Equal("workspaces", result.Value.OwnerKey);
        Assert.Equal(
            TenantTerminationExecutionBoundary.TenantScopedTask,
            result.Value.ExecutionBoundary);
        Assert.NotNull(result.Value.Request);
        Assert.Equal(fixture.Process.ScopeId, result.Value.Request.TenantId);
        Assert.Equal(fixture.Process.CaseId, result.Value.Request.CaseId);
        Assert.Equal(
            fixture.Process.PolicyEvidenceSha256,
            result.Value.Request.PolicyEvidenceSha256);
        Assert.Equal(
            TenantTerminationCoordination.ExecutorActorId,
            result.Value.Request.ExecutingActorId);
        Assert.Equal(
            fixture.Clock.UtcNow.Add(
                BeginTenantTerminationOwnerWorkCommandHandler
                    .OwnerCallTimeout),
            result.Value.Request.DeadlineUtc);
    }

    [Fact]
    public async Task Owner_cannot_start_before_every_dependency_completes()
    {
        Fixture fixture = CreateFixture(
            [Stub("workspaces"), Stub("reservations", ["workspaces"])]);
        TenantTerminationOwnerWorkItem reservations = fixture.Repository
            .WorkItems.Single(item => item.OwnerKey == "reservations");
        Guid runId = TenantTerminationExecutionIdentity.CreateTaskRunId(
            reservations.Id,
            dispatchSequence: 1);

        Result<TenantTerminationOwnerWorkStart> result =
            await fixture.Handler.HandleAsync(
                new(
                    fixture.Process.Id,
                    reservations.Id,
                    fixture.Process.OperationRevision,
                    TenantTerminationContributionPhase.Destroy,
                    reservations.OwnerKey,
                    TenantTerminationExecutionBoundary.TenantScopedTask,
                    runId,
                    TaskAttempt: 1),
                CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(
            DataRightsApplicationErrors
                .TenantTerminationExecutionStateInvalid,
            result.Error);
        Assert.Equal(
            TenantTerminationOwnerWorkState.Prepared,
            reservations.State);
    }

    [Fact]
    public async Task Same_task_run_retry_advances_without_changing_work_identity()
    {
        Fixture fixture = CreateFixture([Stub("reservations")]);
        TenantTerminationPlannedDispatch dispatch = Assert.Single(
            fixture.Planner.FindReadyDispatches(
                fixture.Process,
                fixture.Repository.WorkItems).Value);
        BeginTenantTerminationOwnerWorkCommand firstCommand =
            Command(fixture.Process, dispatch);
        TenantTerminationOwnerWorkStart first = (await fixture.Handler
            .HandleAsync(firstCommand, CancellationToken.None)).Value;
        long firstVersion = first.WorkItemVersion;

        fixture.Clock.UtcNow = fixture.Clock.UtcNow.AddSeconds(10);
        TenantTerminationOwnerWorkStart exactReplay = (await fixture.Handler
            .HandleAsync(firstCommand, CancellationToken.None)).Value;
        Assert.Equal(firstVersion, exactReplay.WorkItemVersion);
        Assert.Equal(first.Request!.DeadlineUtc, exactReplay.Request!.DeadlineUtc);

        fixture.Clock.UtcNow = fixture.Clock.UtcNow.AddMinutes(1);
        Result<TenantTerminationOwnerWorkStart> retry =
            await fixture.Handler.HandleAsync(
                firstCommand with { TaskAttempt = 2 },
                CancellationToken.None);
        Result<TenantTerminationOwnerWorkStart> wrongRun =
            await fixture.Handler.HandleAsync(
                firstCommand with
                {
                    TaskRunId = Guid.NewGuid(),
                    TaskAttempt = 3
                },
                CancellationToken.None);

        Assert.True(retry.IsSuccess);
        Assert.True(retry.Value.DispatchRequired);
        Assert.Equal(firstVersion + 1, retry.Value.WorkItemVersion);
        Assert.Equal(
            fixture.Clock.UtcNow.Add(
                BeginTenantTerminationOwnerWorkCommandHandler
                    .OwnerCallTimeout),
            retry.Value.Request!.DeadlineUtc);
        Assert.True(wrongRun.IsFailure);
    }

    [Fact]
    public async Task Terminal_attempt_replay_does_not_reschedule_or_steal_the_continuation_run()
    {
        Fixture fixture = CreateFixture([Stub("reservations")]);
        TenantTerminationPlannedDispatch firstDispatch = Assert.Single(
            fixture.Planner.FindReadyDispatches(
                fixture.Process,
                fixture.Repository.WorkItems).Value);
        BeginTenantTerminationOwnerWorkCommand firstCommand =
            Command(fixture.Process, firstDispatch);
        _ = await fixture.Handler.HandleAsync(
            firstCommand,
            CancellationToken.None);
        TenantTerminationOwnerWorkItem workItem = Assert.Single(
            fixture.Repository.WorkItems);
        fixture.Clock.UtcNow = fixture.Clock.UtcNow.AddMinutes(1);
        Assert.True(workItem.RecordResult(
            TenantTerminationOwnerWorkState.RetryRequired,
            "reservations.more-work",
            affectedCount: 500,
            retainedMinimumCount: 0,
            remainingActiveCount: 1,
            holdReviewAtUtc: null,
            selectedProofRevision: null,
            resultingProofRevision: null,
            catalogVersion: workItem.CatalogVersion,
            workItem.CatalogSha256,
            firstDispatch.TaskRunId,
            taskAttempt: 1,
            workItem.Version,
            fixture.Clock.UtcNow).IsSuccess);

        TenantTerminationOwnerWorkStart oldReplay = (await fixture.Handler
            .HandleAsync(
                firstCommand with { TaskAttempt = 2 },
                CancellationToken.None)).Value;
        TenantTerminationPlannedDispatch continuation = Assert.Single(
            fixture.Planner.FindReadyDispatches(
                fixture.Process,
                fixture.Repository.WorkItems).Value);
        fixture.Clock.UtcNow = fixture.Clock.UtcNow.AddMinutes(1);
        TenantTerminationOwnerWorkStart continued = (await fixture.Handler
            .HandleAsync(
                Command(fixture.Process, continuation),
                CancellationToken.None)).Value;

        Assert.False(oldReplay.DispatchRequired);
        Assert.Equal(
            TenantTerminationOwnerWorkState.RetryRequired,
            oldReplay.State);
        Assert.Null(oldReplay.Request);
        Assert.Empty(oldReplay.ReadyDispatches);
        Assert.NotEqual(firstDispatch.TaskRunId, continuation.TaskRunId);
        Assert.True(continued.DispatchRequired);
        Assert.Equal(continuation.TaskRunId, workItem.TaskRunId);
    }

    [Fact]
    public async Task Superseded_task_run_uses_authenticated_history_without_invoking_owner_again()
    {
        Fixture fixture = CreateFixture([Stub("reservations")]);
        TenantTerminationPlannedDispatch firstDispatch = Assert.Single(
            fixture.Planner.FindReadyDispatches(
                fixture.Process,
                fixture.Repository.WorkItems).Value);
        BeginTenantTerminationOwnerWorkCommand firstCommand =
            Command(fixture.Process, firstDispatch);
        TenantTerminationOwnerWorkStart first = (await fixture.Handler
            .HandleAsync(firstCommand, CancellationToken.None)).Value;
        DateTimeOffset resultAtUtc = fixture.Clock.UtcNow.AddSeconds(10);
        TenantTerminationContributionResult retryRequired = new(
            TenantTerminationContributionStatus.RetryRequired,
            "reservations.more-work",
            AffectedCount: 500,
            RetainedMinimumCount: 0,
            RemainingActiveCount: 1,
            HoldReviewAtUtc: null,
            SelectedProofRevision: null,
            ResultingProofRevision: null,
            first.CatalogVersion,
            first.CatalogSha256,
            resultAtUtc);
        TenantTerminationReplayDispatch protectedDispatch =
            TenantTerminationReplayDispatch.Create(
                first.Request!,
                first.OwnerKey,
                first.CatalogVersion,
                first.CatalogSha256,
                first.ExecutionBoundary,
                firstDispatch.TaskRunId,
                taskAttempt: 1,
                fixture.Clock.UtcNow.AddSeconds(1));
        fixture.ReplayStore.Add(new(
            protectedDispatch,
            TenantTerminationReplayResult.Create(
                protectedDispatch,
                retryRequired,
                resultAtUtc.AddSeconds(1))));
        TenantTerminationOwnerWorkItem workItem = Assert.Single(
            fixture.Repository.WorkItems);
        Assert.True(workItem.RecordResult(
            TenantTerminationOwnerWorkState.RetryRequired,
            retryRequired.ResultCode,
            retryRequired.AffectedCount,
            retryRequired.RetainedMinimumCount,
            retryRequired.RemainingActiveCount,
            retryRequired.HoldReviewAtUtc,
            retryRequired.SelectedProofRevision,
            retryRequired.ResultingProofRevision,
            retryRequired.CatalogVersion,
            retryRequired.CatalogSha256,
            firstDispatch.TaskRunId,
            taskAttempt: 1,
            workItem.Version,
            retryRequired.RecordedAtUtc).IsSuccess);
        TenantTerminationPlannedDispatch continuation = Assert.Single(
            fixture.Planner.FindReadyDispatches(
                fixture.Process,
                fixture.Repository.WorkItems).Value);
        fixture.Clock.UtcNow = resultAtUtc.AddMinutes(1);
        _ = (await fixture.Handler.HandleAsync(
            Command(fixture.Process, continuation),
            CancellationToken.None)).Value;
        long continuationVersion = workItem.Version;

        TenantTerminationOwnerWorkStart replay = (await fixture.Handler
            .HandleAsync(
                firstCommand with { TaskAttempt = 2 },
                CancellationToken.None)).Value;

        Assert.False(replay.DispatchRequired);
        Assert.Empty(replay.ReadyDispatches);
        Assert.Equal(continuationVersion, workItem.Version);
        Assert.Equal(continuation.TaskRunId, workItem.TaskRunId);
        Assert.Equal(TenantTerminationOwnerWorkState.Processing, workItem.State);
    }

    [Fact]
    public async Task Incomplete_live_work_set_fails_closed()
    {
        Fixture fixture = CreateFixture(
            [Stub("workspaces"), Stub("reservations", ["workspaces"])]);
        TenantTerminationPlannedDispatch dispatch = Assert.Single(
            fixture.Planner.FindReadyDispatches(
                fixture.Process,
                fixture.Repository.WorkItems).Value);
        fixture.Repository.WorkItems.RemoveAt(1);

        Result<TenantTerminationOwnerWorkStart> result =
            await fixture.Handler.HandleAsync(
                Command(fixture.Process, dispatch),
                CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(
            DataRightsApplicationErrors
                .TenantTerminationExecutionStateInvalid,
            result.Error);
    }

    private static Fixture CreateFixture(
        IReadOnlyCollection<ITenantTerminationContributor> contributors)
    {
        TenantTerminationProcess process = CreateRunningDestroyProcess();
        TenantTerminationPhasePlanner planner = new(contributors);
        List<TenantTerminationOwnerWorkItem> workItems =
            [.. planner.PrepareWorkItems(process, Now.AddMinutes(3)).Value];
        StubRepository repository = new(process, workItems);
        MutableClock clock = new(Now.AddMinutes(4));
        StubTenantTerminationReplayStore replayStore = new();
        return new(
            process,
            planner,
            repository,
            replayStore,
            clock,
            new(repository, planner, replayStore, clock));
    }

    private static BeginTenantTerminationOwnerWorkCommand Command(
        TenantTerminationProcess process,
        TenantTerminationPlannedDispatch dispatch) =>
        new(
            process.Id,
            dispatch.WorkItemId,
            dispatch.OperationRevision,
            dispatch.Phase,
            dispatch.OwnerKey,
            dispatch.ExecutionBoundary,
            dispatch.TaskRunId,
            TaskAttempt: 1);

    private static TenantTerminationProcess CreateRunningDestroyProcess()
    {
        TenantTerminationProcess process = TenantTerminationProcess.Prepare(
            Guid.NewGuid(),
            "tenant-a",
            Guid.NewGuid(),
            Guid.NewGuid(),
            approvalRevision: 4,
            Guid.NewGuid(),
            exportRequested: false,
            Digest,
            "approver",
            Now,
            "creator",
            Now).Value;
        Assert.True(process.BeginPhase(
            TenantTerminationProcessPhase.Freeze,
            process.Version,
            "executor",
            Now.AddMinutes(1)).IsSuccess);
        Assert.True(TenantTerminationTestFixture.CompleteFreeze(
            process,
            process.OperationRevision,
            process.Version,
            "executor",
            Now.AddMinutes(2)).IsSuccess);
        Assert.True(process.BeginPhase(
            TenantTerminationProcessPhase.Destroy,
            process.Version,
            "executor",
            Now.AddMinutes(3)).IsSuccess);
        return process;
    }

    private static StubContributor Stub(
        string ownerKey,
        IReadOnlyCollection<string>? dependencies = null) =>
        new(new(
            ownerKey,
            TenantTerminationContract.CurrentVersion,
            [
                new(
                    TenantTerminationContributionPhase.Destroy,
                    dependencies ?? [])
            ],
            MandatoryForProduction: true,
            CatalogVersion: 2,
            CatalogSha256: Digest));

    private sealed class StubContributor(
        TenantTerminationContributorDescriptor descriptor)
        : ITenantTerminationContributor
    {
        public TenantTerminationContributorDescriptor Descriptor { get; } =
            descriptor;

        public Task<TenantTerminationContributionResult> ExecuteAsync(
            TenantTerminationContributionRequest request,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class StubRepository(
        TenantTerminationProcess process,
        List<TenantTerminationOwnerWorkItem> workItems)
        : ITenantTerminationRepository
    {
        public List<TenantTerminationOwnerWorkItem> WorkItems { get; } =
            workItems;

        public Task AddProcessAsync(
            TenantTerminationProcess value,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task AddOwnerWorkItemAsync(
            TenantTerminationOwnerWorkItem workItem,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<TenantTerminationProcess?> GetProcessAsync(
            Guid processId,
            CancellationToken cancellationToken) =>
            Task.FromResult<TenantTerminationProcess?>(
                process.Id == processId ? process : null);

        public Task<TenantTerminationProcess?> GetActiveProcessAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<TenantTerminationProcess?>(process);

        public Task<TenantTerminationProcess?>
            GetProcessByIdempotencyKeyAsync(
                Guid idempotencyKey,
                CancellationToken cancellationToken) =>
            Task.FromResult<TenantTerminationProcess?>(
                process.IdempotencyKey == idempotencyKey ? process : null);

        public Task<TenantTerminationOwnerWorkItem?> GetOwnerWorkItemAsync(
            Guid processId,
            TenantTerminationOwnerPhase phase,
            string ownerKey,
            long operationRevision,
            CancellationToken cancellationToken) =>
            Task.FromResult(this.WorkItems.SingleOrDefault(item =>
                item.ProcessId == processId &&
                item.Phase == phase &&
                item.OwnerKey == ownerKey &&
                item.OperationRevision == operationRevision));

        public Task<IReadOnlyList<TenantTerminationOwnerWorkItem>>
            ListOwnerWorkItemsAsync(
                Guid processId,
                TenantTerminationOwnerPhase phase,
                long operationRevision,
                CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TenantTerminationOwnerWorkItem>>(
                this.WorkItems
                    .Where(item =>
                        item.ProcessId == processId &&
                        item.Phase == phase &&
                        item.OperationRevision == operationRevision)
                    .OrderBy(item => item.OwnerKey, StringComparer.Ordinal)
                    .ToArray());
    }

    private sealed class MutableClock(DateTimeOffset utcNow) : ISystemClock
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;
    }

    private sealed record Fixture(
        TenantTerminationProcess Process,
        TenantTerminationPhasePlanner Planner,
        StubRepository Repository,
        StubTenantTerminationReplayStore ReplayStore,
        MutableClock Clock,
        BeginTenantTerminationOwnerWorkCommandHandler Handler);
}

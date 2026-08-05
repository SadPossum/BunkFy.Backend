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
public sealed class RecordTenantTerminationOwnerResultCommandHandlerTests
{
    private static readonly string Digest = new('a', 64);
    private static readonly DateTimeOffset Now =
        new(2026, 8, 4, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Authenticated_completion_releases_dependent_owner()
    {
        Fixture fixture = await CreateFixtureAsync(
            [Stub("reservations"), Stub("guests", ["reservations"])]);
        TenantTerminationReplayAttempt proof = CompletedProof(fixture);
        fixture.ReplayStore.Attempt = proof;
        RecordTenantTerminationOwnerResultCommand command =
            Command(fixture);

        Result<TenantTerminationOwnerResultRecorded> result =
            await fixture.Handler.HandleAsync(
                command,
                CancellationToken.None);
        long completedVersion = fixture.WorkItem.Version;
        Result<TenantTerminationOwnerResultRecorded> exactReplay =
            await fixture.Handler.HandleAsync(
                command,
                CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            TenantTerminationOwnerWorkState.Completed,
            result.Value.State);
        TenantTerminationPlannedDispatch next =
            Assert.Single(result.Value.ReadyDispatches);
        Assert.Equal("guests", next.OwnerKey);
        Assert.True(exactReplay.IsSuccess);
        Assert.Equal(completedVersion, fixture.WorkItem.Version);
        Assert.Single(fixture.Signal.Captures);
    }

    [Fact]
    public async Task Dispatch_without_an_authenticated_result_cannot_commit()
    {
        Fixture fixture = await CreateFixtureAsync([Stub("reservations")]);
        TenantTerminationReplayAttempt completed = CompletedProof(fixture);
        fixture.ReplayStore.Attempt = new(
            completed.Dispatch,
            Result: null);

        Result<TenantTerminationOwnerResultRecorded> result =
            await fixture.Handler.HandleAsync(
                Command(fixture),
                CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(
            DataRightsApplicationErrors
                .TenantTerminationExecutionStateInvalid,
            result.Error);
        Assert.Equal(
            TenantTerminationOwnerWorkState.Processing,
            fixture.WorkItem.State);
        Assert.Empty(fixture.Signal.Captures);
    }

    [Fact]
    public async Task Valid_but_foreign_dispatch_proof_fails_closed()
    {
        Fixture fixture = await CreateFixtureAsync([Stub("reservations")]);
        TenantTerminationContributionRequest foreignRequest =
            fixture.Start.Request! with
            {
                ExecutingActorId = "system:other-executor"
            };
        TenantTerminationReplayDispatch dispatch =
            TenantTerminationReplayDispatch.Create(
                foreignRequest,
                fixture.Start.OwnerKey,
                fixture.Start.CatalogVersion,
                fixture.Start.CatalogSha256,
                fixture.Start.ExecutionBoundary,
                fixture.Dispatch.TaskRunId,
                taskAttempt: 1,
                Now.AddMinutes(4).AddSeconds(1));
        TenantTerminationReplayResult result =
            TenantTerminationReplayResult.Create(
                dispatch,
                CompletedContribution(fixture.Start),
                Now.AddMinutes(4).AddSeconds(11));
        fixture.ReplayStore.Attempt = new(dispatch, result);

        Result<TenantTerminationOwnerResultRecorded> recorded =
            await fixture.Handler.HandleAsync(
                Command(fixture),
                CancellationToken.None);

        Assert.True(recorded.IsFailure);
        Assert.Equal(
            TenantTerminationOwnerWorkState.Processing,
            fixture.WorkItem.State);
    }

    [Fact]
    public async Task Retry_result_releases_a_new_deterministic_task_run()
    {
        Fixture fixture = await CreateFixtureAsync([Stub("reservations")]);
        TenantTerminationReplayDispatch dispatch = CreateDispatch(fixture);
        TenantTerminationContributionResult contribution = new(
            TenantTerminationContributionStatus.RetryRequired,
            "reservations.more-work",
            AffectedCount: 500,
            RetainedMinimumCount: 0,
            RemainingActiveCount: 1,
            HoldReviewAtUtc: null,
            SelectedProofRevision: null,
            ResultingProofRevision: null,
            fixture.Start.CatalogVersion,
            fixture.Start.CatalogSha256,
            Now.AddMinutes(4).AddSeconds(10));
        fixture.ReplayStore.Attempt = new(
            dispatch,
            TenantTerminationReplayResult.Create(
                dispatch,
                contribution,
                Now.AddMinutes(4).AddSeconds(11)));

        TenantTerminationOwnerResultRecorded recorded =
            (await fixture.Handler.HandleAsync(
                Command(fixture),
                CancellationToken.None)).Value;

        Assert.Equal(
            TenantTerminationOwnerWorkState.RetryRequired,
            recorded.State);
        TenantTerminationPlannedDispatch continuation = Assert.Single(
            recorded.ReadyDispatches);
        Assert.Equal(fixture.WorkItem.Id, continuation.WorkItemId);
        Assert.NotEqual(fixture.Dispatch.TaskRunId, continuation.TaskRunId);
        Assert.Equal(2, continuation.DispatchSequence);
    }

    private static async Task<Fixture> CreateFixtureAsync(
        IReadOnlyCollection<ITenantTerminationContributor> contributors)
    {
        TenantTerminationProcess process = CreateRunningDestroyProcess();
        TenantTerminationPhasePlanner planner = new(contributors);
        List<TenantTerminationOwnerWorkItem> workItems =
            [.. planner.PrepareWorkItems(process, Now.AddMinutes(3)).Value];
        StubRepository repository = new(process, workItems);
        FixedClock clock = new(Now.AddMinutes(4));
        TenantTerminationPlannedDispatch dispatch = Assert.Single(
            planner.FindReadyDispatches(process, workItems).Value);
        TenantTerminationOwnerWorkStart start = (await new
            BeginTenantTerminationOwnerWorkCommandHandler(
                repository,
                planner,
                new StubTenantTerminationReplayStore(),
                clock).HandleAsync(
                    new(
                        process.Id,
                        dispatch.WorkItemId,
                        dispatch.OperationRevision,
                        dispatch.Phase,
                        dispatch.OwnerKey,
                        dispatch.ExecutionBoundary,
                        dispatch.TaskRunId,
                        TaskAttempt: 1),
                    CancellationToken.None)).Value;
        StubReplayStore replayStore = new();
        RecordingTenantTerminationCoordinationSignal signal = new();
        return new(
            process,
            planner,
            repository,
            replayStore,
            dispatch,
            workItems.Single(item => item.Id == dispatch.WorkItemId),
            start,
            signal,
            new(
                repository,
                replayStore,
                planner,
                signal));
    }

    private static RecordTenantTerminationOwnerResultCommand Command(
        Fixture fixture) =>
        new(
            fixture.Process.Id,
            fixture.WorkItem.Id,
            fixture.Process.OperationRevision,
            fixture.Dispatch.Phase,
            fixture.Dispatch.OwnerKey,
            fixture.Dispatch.TaskRunId,
            TaskAttempt: 1,
            fixture.Start.WorkItemVersion);

    private static TenantTerminationReplayAttempt CompletedProof(
        Fixture fixture)
    {
        TenantTerminationReplayDispatch dispatch = CreateDispatch(fixture);
        return new(
            dispatch,
            TenantTerminationReplayResult.Create(
                dispatch,
                CompletedContribution(fixture.Start),
                Now.AddMinutes(4).AddSeconds(11)));
    }

    private static TenantTerminationReplayDispatch CreateDispatch(
        Fixture fixture) =>
        TenantTerminationReplayDispatch.Create(
            fixture.Start.Request!,
            fixture.Start.OwnerKey,
            fixture.Start.CatalogVersion,
            fixture.Start.CatalogSha256,
            fixture.Start.ExecutionBoundary,
            fixture.Dispatch.TaskRunId,
            taskAttempt: 1,
            Now.AddMinutes(4).AddSeconds(1));

    private static TenantTerminationContributionResult CompletedContribution(
        TenantTerminationOwnerWorkStart start) =>
        new(
            TenantTerminationContributionStatus.Completed,
            $"{start.OwnerKey}.termination.destroyed",
            AffectedCount: 12,
            RetainedMinimumCount: 2,
            RemainingActiveCount: 0,
            HoldReviewAtUtc: null,
            SelectedProofRevision: 4,
            ResultingProofRevision: 5,
            start.CatalogVersion,
            start.CatalogSha256,
            Now.AddMinutes(4).AddSeconds(10));

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

    private sealed class StubReplayStore : ITenantTerminationReplayStore
    {
        public TenantTerminationReplayAttempt? Attempt { get; set; }

        public Task<TenantTerminationReplayStoreReadiness> CheckReadinessAsync(
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<TenantTerminationReplayAppendReceipt> AppendAsync(
            TenantTerminationReplayJournalEntry entry,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<TenantTerminationReplayAttempt?> ReadAttemptAsync(
            TenantTerminationReplayAttemptCoordinate coordinate,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                this.Attempt?.Dispatch.Coordinate == coordinate
                    ? this.Attempt
                    : null);

        public Task<TenantTerminationReplayIntent?> ReadIntentAsync(
            string tenantId,
            Guid processId,
            CancellationToken cancellationToken) =>
            Task.FromResult<TenantTerminationReplayIntent?>(null);

        public Task<TenantTerminationReplayCheckpoint>
            ReadTrustedCheckpointAsync(
                string tenantId,
                Guid processId,
                CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<TenantTerminationReplayPage> ReadAfterAsync(
            string tenantId,
            Guid processId,
            TenantTerminationReplayCursor cursor,
            int pageSize,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class StubRepository(
        TenantTerminationProcess process,
        List<TenantTerminationOwnerWorkItem> workItems)
        : ITenantTerminationRepository
    {
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
            Task.FromResult(workItems.SingleOrDefault(item =>
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
                workItems
                    .Where(item =>
                        item.ProcessId == processId &&
                        item.Phase == phase &&
                        item.OperationRevision == operationRevision)
                    .OrderBy(item => item.OwnerKey, StringComparer.Ordinal)
                    .ToArray());
    }

    private sealed class FixedClock(DateTimeOffset utcNow) : ISystemClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }

    private sealed record Fixture(
        TenantTerminationProcess Process,
        TenantTerminationPhasePlanner Planner,
        StubRepository Repository,
        StubReplayStore ReplayStore,
        TenantTerminationPlannedDispatch Dispatch,
        TenantTerminationOwnerWorkItem WorkItem,
        TenantTerminationOwnerWorkStart Start,
        RecordingTenantTerminationCoordinationSignal Signal,
        RecordTenantTerminationOwnerResultCommandHandler Handler);
}

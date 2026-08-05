namespace BunkFy.Modules.DataRights.Tests.Application;

using BunkFy.Modules.DataRights.Application.Commands;
using BunkFy.Modules.DataRights.Application.Handlers;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Application.Tasks;
using BunkFy.Modules.DataRights.Contracts;
using Gma.Framework.Cqrs;
using Gma.Framework.Messaging;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Tasks;
using Gma.Framework.Tasks.Cqrs;
using Xunit;

[Trait("Category", "Unit")]
public sealed class DataRightsResponseDeadlineAlertTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 5, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Dispatch_publishes_only_the_minimized_deadline_contract()
    {
        Guid dispatchId = Guid.NewGuid();
        Guid caseId = Guid.NewGuid();
        Guid propertyId = Guid.NewGuid();
        RecordingAlertRepository repository = new([
            new(
                dispatchId,
                "tenant-a",
                caseId,
                propertyId,
                DataRightsResponseDeadlineAlertKind.DueSoon,
                Now.AddHours(24))
        ]);
        RecordingOutbox outbox = new();
        DispatchDataRightsResponseDeadlineAlertsCommandHandler handler = new(
            repository,
            new RecordingOutboxRegistry(outbox),
            new TestClock());

        Result<DataRightsResponseDeadlineAlertDispatchBatchResult> result =
            await handler.HandleAsync(new(100), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error.Code);
        Assert.Equal(1, result.Value.DispatchedCount);
        Assert.Equal(Now, repository.ClaimedAtUtc);
        Assert.Equal(Now.AddHours(48), repository.DueSoonUntilUtc);
        DataRightsResponseDeadlineAlertDueIntegrationEvent integrationEvent =
            Assert.IsType<DataRightsResponseDeadlineAlertDueIntegrationEvent>(
                Assert.Single(outbox.Events));
        Assert.Equal(dispatchId, integrationEvent.EventId);
        Assert.Equal(caseId, integrationEvent.CaseId);
        Assert.Equal(propertyId, integrationEvent.PropertyId);
        Assert.Equal(
            DataRightsResponseDeadlineAlertKind.DueSoon,
            integrationEvent.AlertKind);
        Assert.DoesNotContain(
            integrationEvent.GetType().GetProperties(),
            property => property.Name is
                "RequesterName" or "Email" or "Phone" or "RequestedRights" or
                "Notes" or "PolicyEvidence");
    }

    [Fact]
    public async Task Schedule_provider_creates_one_minute_scoped_schedule()
    {
        DataRightsResponseDeadlineAlertScheduleProvider provider = new(
            new RecordingAlertRepository([], ["tenant-b", "tenant-a"]));

        ScheduledTaskDefinition[] schedules = await provider
            .GetSchedulesAsync(CancellationToken.None)
            .ToArrayAsync(CancellationToken.None);

        Assert.Equal(2, schedules.Length);
        Assert.Equal(["tenant-b", "tenant-a"], schedules.Select(item => item.ScopeId));
        Assert.All(schedules, schedule =>
        {
            Assert.Equal(
                DispatchDataRightsResponseDeadlineAlertsPayload.TaskName,
                schedule.TaskName);
            Assert.Equal(TimeSpan.FromMinutes(1), schedule.Interval);
            Assert.Equal(
                DataRightsModuleMetadata.DeadlineAlertWorkerGroup,
                schedule.WorkerGroup);
            Assert.True(schedule.RunOnStart);
            Assert.Equal(3, schedule.MaxAttempts);
        });
    }

    [Fact]
    public async Task Task_stops_after_the_first_short_batch()
    {
        RecordingTaskDispatcher dispatcher = new(100, 3);
        DispatchDataRightsResponseDeadlineAlertsTaskHandler handler = new(
            dispatcher);

        await handler.HandleAsync(
            new(BatchSize: 100, MaxBatches: 4),
            Context(),
            CancellationToken.None);

        Assert.Equal(2, dispatcher.DispatchCount);
    }

    private static TaskExecutionContext Context() => new(
        Guid.NewGuid(),
        DataRightsModuleMetadata.Name,
        DispatchDataRightsResponseDeadlineAlertsPayload.TaskName,
        DataRightsModuleMetadata.DeadlineAlertWorkerGroup,
        "worker-1",
        "node-1",
        attempt: 1,
        scopeId: "tenant-a",
        correlationId: Guid.NewGuid());

    private sealed class RecordingAlertRepository(
        IReadOnlyList<DataRightsResponseDeadlineAlertDispatch> dispatches,
        IReadOnlyList<string>? scopeIds = null)
        : IDataRightsResponseDeadlineAlertRepository
    {
        public DateTimeOffset? ClaimedAtUtc { get; private set; }
        public DateTimeOffset? DueSoonUntilUtc { get; private set; }

        public Task<DataRightsResponseDeadlineAlertClaimResult> ClaimAsync(
            DateTimeOffset nowUtc,
            DateTimeOffset dueSoonUntilUtc,
            int batchSize,
            CancellationToken cancellationToken)
        {
            this.ClaimedAtUtc = nowUtc;
            this.DueSoonUntilUtc = dueSoonUntilUtc;
            return Task.FromResult(new DataRightsResponseDeadlineAlertClaimResult(
                dispatches.Count,
                dispatches));
        }

        public Task<IReadOnlyList<string>> ListScheduleScopeIdsAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<string>>(scopeIds ?? []);
    }

    private sealed class RecordingOutbox : IOutboxWriter
    {
        public string ModuleName => DataRightsModuleMetadata.Name;
        public List<IIntegrationEvent> Events { get; } = [];

        public Task EnqueueAsync<TEvent>(
            TEvent integrationEvent,
            CancellationToken cancellationToken)
            where TEvent : IIntegrationEvent
        {
            this.Events.Add(integrationEvent);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingOutboxRegistry(RecordingOutbox outbox)
        : IOutboxWriterRegistry
    {
        public IOutboxWriter GetRequired(string moduleName)
        {
            Assert.Equal(DataRightsModuleMetadata.Name, moduleName);
            return outbox;
        }
    }

    private sealed class RecordingTaskDispatcher(params int[] processedCounts)
        : ITaskCommandDispatcher
    {
        private int index;
        public int DispatchCount { get; private set; }

        public Task<Result<TResponse>> DispatchAsync<TCommand, TResponse>(
            TaskExecutionContext context,
            TCommand command,
            CancellationToken cancellationToken)
            where TCommand : ICommand<TResponse>
        {
            Assert.IsType<DispatchDataRightsResponseDeadlineAlertsCommand>(command);
            this.DispatchCount++;
            int processed = processedCounts[this.index++];
            object result = Result.Success(
                new DataRightsResponseDeadlineAlertDispatchBatchResult(
                    processed,
                    processed));
            return Task.FromResult((Result<TResponse>)result);
        }
    }

    private sealed class TestClock : ISystemClock
    {
        public DateTimeOffset UtcNow => Now;
    }
}

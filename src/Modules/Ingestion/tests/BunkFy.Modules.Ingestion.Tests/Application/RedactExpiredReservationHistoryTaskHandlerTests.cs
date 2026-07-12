namespace BunkFy.Modules.Ingestion.Tests.Application;

using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Tasks;
using Gma.Framework.Tasks.Cqrs;
using BunkFy.Modules.Ingestion.Application.Commands;
using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Application.Tasks;
using BunkFy.Modules.Ingestion.Contracts;
using Xunit;

[Trait("Category", "Unit")]
public sealed class RedactExpiredReservationHistoryTaskHandlerTests
{
    [Fact]
    public async Task Handler_runs_bounded_batches_until_a_partial_batch()
    {
        FakeDispatcher dispatcher = new([3, 3, 1]);
        RedactExpiredReservationHistoryTaskHandler handler = new(dispatcher);

        await handler.HandleAsync(
            new RedactExpiredReservationHistoryPayload(BatchSize: 3, MaxBatches: 5),
            Context(),
            CancellationToken.None);

        Assert.Equal(3, dispatcher.Commands.Count);
        Assert.All(dispatcher.Commands, command => Assert.Equal(3, command.BatchSize));
    }

    [Fact]
    public async Task Handler_rejects_unscoped_or_unbounded_execution()
    {
        RedactExpiredReservationHistoryTaskHandler handler = new(new FakeDispatcher([]));

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(
            new RedactExpiredReservationHistoryPayload(BatchSize: 0),
            Context(),
            CancellationToken.None));
    }

    private static TaskExecutionContext Context() => new(
        Guid.NewGuid(),
        IngestionModuleMetadata.Name,
        RedactExpiredReservationHistoryPayload.TaskName,
        IngestionModuleMetadata.MaintenanceWorkerGroup,
        "worker-1",
        "node-1",
        attempt: 1,
        scopeId: "tenant-a",
        leaseExtension: TimeSpan.FromMinutes(5));

    private sealed class FakeDispatcher(IEnumerable<int> totals) : ITaskCommandDispatcher
    {
        private readonly Queue<int> totals = new(totals);
        public List<RedactExpiredSensitiveHistoryCommand> Commands { get; } = [];

        public Task<Result<TResponse>> DispatchAsync<TCommand, TResponse>(
            TaskExecutionContext context,
            TCommand command,
            CancellationToken cancellationToken)
            where TCommand : ICommand<TResponse>
        {
            RedactExpiredSensitiveHistoryCommand redaction =
                Assert.IsType<RedactExpiredSensitiveHistoryCommand>(command);
            this.Commands.Add(redaction);
            int total = this.totals.Dequeue();
            object result = Result.Success(new SensitiveHistoryRedactionBatchResult(total, 0));
            return Task.FromResult((Result<TResponse>)result);
        }
    }
}

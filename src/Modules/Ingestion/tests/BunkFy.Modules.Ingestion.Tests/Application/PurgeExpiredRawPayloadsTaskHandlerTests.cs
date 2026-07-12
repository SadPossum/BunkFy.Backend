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
public sealed class PurgeExpiredRawPayloadsTaskHandlerTests
{
    [Fact]
    public async Task Missing_object_is_idempotent_and_receipt_is_finalized()
    {
        RawPayloadPurgeCandidate candidate = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        FakeDispatcher dispatcher = new(candidate);
        FakeRawPayloadStore payloads = new(deleteResult: false);
        PurgeExpiredRawPayloadsTaskHandler handler = new(dispatcher, payloads);
        TaskExecutionContext context = Context();

        await handler.HandleAsync(new(), context, CancellationToken.None);

        RawPayloadPurgeCandidate deleted = Assert.Single(payloads.Deletes);
        Assert.Equal(candidate.RawPayloadFileId, deleted.RawPayloadFileId);
        Assert.Equal(candidate.ConnectionId, deleted.ConnectionId);
        CompleteRawPayloadPurgeCommand completed = Assert.Single(dispatcher.Completions);
        Assert.Equal(candidate.ReceiptId, completed.ReceiptId);
        Assert.Equal(context.RunId, completed.ClaimId);
    }

    [Fact]
    public async Task Storage_failure_leaves_claim_unfinished_and_same_task_retry_can_recover()
    {
        RawPayloadPurgeCandidate candidate = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        FakeDispatcher dispatcher = new(candidate);
        FakeRawPayloadStore payloads = new(deleteResult: true, failFirstDelete: true);
        PurgeExpiredRawPayloadsTaskHandler handler = new(dispatcher, payloads);
        TaskExecutionContext context = Context();

        await Assert.ThrowsAsync<IOException>(() => handler.HandleAsync(new(), context, CancellationToken.None));
        Assert.Empty(dispatcher.Completions);

        await handler.HandleAsync(new(), context, CancellationToken.None);

        Assert.Equal(2, payloads.DeleteAttempts);
        Assert.Single(dispatcher.Completions);
        Assert.All(dispatcher.Claims, claim => Assert.Equal(context.RunId, claim.ClaimId));
    }

    private static TaskExecutionContext Context() => new(
        Guid.NewGuid(),
        IngestionModuleMetadata.Name,
        PurgeExpiredRawPayloadsPayload.TaskName,
        IngestionModuleMetadata.MaintenanceWorkerGroup,
        "worker-1",
        "node-1",
        attempt: 1,
        scopeId: "tenant-a",
        leaseExtension: TimeSpan.FromMinutes(5));

    private sealed class FakeDispatcher(RawPayloadPurgeCandidate candidate) : ITaskCommandDispatcher
    {
        private bool completed;
        public List<ClaimExpiredRawPayloadsCommand> Claims { get; } = [];
        public List<CompleteRawPayloadPurgeCommand> Completions { get; } = [];

        public Task<Result<TResponse>> DispatchAsync<TCommand, TResponse>(
            TaskExecutionContext context,
            TCommand command,
            CancellationToken cancellationToken)
            where TCommand : ICommand<TResponse>
        {
            object result = command switch
            {
                ClaimExpiredRawPayloadsCommand claim => this.Claim(claim),
                CompleteRawPayloadPurgeCommand completion => this.Complete(completion),
                _ => throw new InvalidOperationException($"Unexpected command {command.GetType().Name}.")
            };
            return Task.FromResult((Result<TResponse>)result);
        }

        private Result<IReadOnlyList<RawPayloadPurgeCandidate>> Claim(ClaimExpiredRawPayloadsCommand command)
        {
            this.Claims.Add(command);
            return Result.Success<IReadOnlyList<RawPayloadPurgeCandidate>>(
                this.completed ? [] : [candidate]);
        }

        private Result<Unit> Complete(CompleteRawPayloadPurgeCommand command)
        {
            this.Completions.Add(command);
            this.completed = true;
            return Result.Success(Unit.Value);
        }
    }

    private sealed class FakeRawPayloadStore(bool deleteResult, bool failFirstDelete = false) : IRawPayloadStore
    {
        public int DeleteAttempts { get; private set; }
        public List<RawPayloadPurgeCandidate> Deletes { get; } = [];

        public Task StoreAsync(RawPayloadWrite write, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<RawPayloadRead?> ReadAsync(
            Guid payloadId,
            string scopeId,
            Guid connectionId,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<bool> DeleteAsync(
            Guid payloadId,
            string scopeId,
            Guid connectionId,
            CancellationToken cancellationToken)
        {
            this.DeleteAttempts++;
            if (failFirstDelete && this.DeleteAttempts == 1)
            {
                throw new IOException("simulated storage outage");
            }

            this.Deletes.Add(new RawPayloadPurgeCandidate(Guid.Empty, payloadId, connectionId));
            return Task.FromResult(deleteResult);
        }
    }
}

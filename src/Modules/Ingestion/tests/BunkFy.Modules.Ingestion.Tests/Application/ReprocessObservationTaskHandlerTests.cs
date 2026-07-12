namespace BunkFy.Modules.Ingestion.Tests.Application;

using BunkFy.Adapter.Abstractions;
using BunkFy.ObservationParsing;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Tasks;
using Gma.Framework.Tasks.Cqrs;
using BunkFy.Modules.Ingestion.Application;
using BunkFy.Modules.Ingestion.Application.Commands;
using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Contracts;
using BunkFy.Modules.Ingestion.Domain.Reprocessing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

[Trait("Category", "Unit")]
public sealed class ReprocessObservationTaskHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 12, 11, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Parsed_output_reenters_authoritative_receipt_path_and_records_lineage()
    {
        TaskExecutionContext context = Context();
        FakeTaskCommandDispatcher dispatcher = new(context.RunId);
        FakeParser parser = new(noMatch: false, throws: false);
        ITaskHandler<ReprocessObservationPayload> handler = CreateHandler(dispatcher, parser);

        await handler.HandleAsync(
            new ReprocessObservationPayload(context.RunId, parser.Descriptor.ParserType, 1, 3),
            context,
            CancellationToken.None);

        ReceiveObservationCommand received = Assert.Single(dispatcher.Received);
        Assert.Equal(dispatcher.Start.SourceReceiptId, received.SourceReceiptId);
        Assert.Equal(context.RunId, received.ReprocessingAttemptId);
        Assert.Equal(parser.Descriptor.ParserType, received.ParserType);
        Assert.Equal(0, received.ParserOutputIndex);
        RecordObservationReprocessingOutputCommand output = Assert.Single(dispatcher.Outputs);
        Assert.Equal(ObservationReprocessingOutputDisposition.Accepted, output.Disposition);
        CompleteObservationReprocessingCommand completed = Assert.Single(dispatcher.Completions);
        Assert.Equal(1, completed.AcceptedCount);
        Assert.Equal(0, completed.RejectedCount);
    }

    [Fact]
    public async Task No_match_is_terminal_without_creating_a_derived_receipt()
    {
        TaskExecutionContext context = Context();
        FakeTaskCommandDispatcher dispatcher = new(context.RunId);
        FakeParser parser = new(noMatch: true, throws: false);
        ITaskHandler<ReprocessObservationPayload> handler = CreateHandler(dispatcher, parser);

        await handler.HandleAsync(
            new ReprocessObservationPayload(context.RunId, parser.Descriptor.ParserType, 1, 3),
            context,
            CancellationToken.None);

        Assert.Empty(dispatcher.Received);
        CompleteObservationReprocessingCommand completed = Assert.Single(dispatcher.Completions);
        Assert.True(completed.NoMatch);
        Assert.Equal("test.parser.no-match", completed.ReasonCode);
    }

    [Fact]
    public async Task Parser_exception_records_retryable_attempt_without_leaking_exception_text()
    {
        TaskExecutionContext context = Context();
        FakeTaskCommandDispatcher dispatcher = new(context.RunId);
        FakeParser parser = new(noMatch: false, throws: true);
        ITaskHandler<ReprocessObservationPayload> handler = CreateHandler(dispatcher, parser);

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(
            new ReprocessObservationPayload(context.RunId, parser.Descriptor.ParserType, 1, 3),
            context,
            CancellationToken.None));

        ScheduleObservationReprocessingRetryCommand retry = Assert.Single(dispatcher.Retries);
        Assert.Equal("ingestion.reprocessing-execution-failed", retry.ErrorCode);
        Assert.DoesNotContain("sensitive", retry.ErrorCode, StringComparison.OrdinalIgnoreCase);
    }

    private static ITaskHandler<ReprocessObservationPayload> CreateHandler(
        FakeTaskCommandDispatcher dispatcher,
        FakeParser parser)
    {
        byte[] raw = "raw-message"u8.ToArray();
        ServiceCollection services = new();
        services.AddSingleton<ITaskCommandDispatcher>(dispatcher);
        services.AddSingleton<IObservationParserDescriptorProvider>(parser);
        services.AddSingleton<IObservationParser>(parser);
        services.AddSingleton<IRawPayloadStore>(new FakeRawPayloadStore(raw));
        services.AddIngestionApplication();
        services.AddIngestionTaskHandlers();
        ServiceProvider provider = services.BuildServiceProvider();
        TaskHandlerRegistration registration = provider.GetRequiredService<ITaskHandlerRegistry>()
            .Find(IngestionModuleMetadata.Name, ReprocessObservationPayload.TaskName)!;
        return (ITaskHandler<ReprocessObservationPayload>)provider.GetRequiredService(registration.HandlerType);
    }

    private static TaskExecutionContext Context() => new(
        Guid.NewGuid(),
        IngestionModuleMetadata.Name,
        ReprocessObservationPayload.TaskName,
        IngestionModuleMetadata.MaintenanceWorkerGroup,
        "worker-1",
        "node-1",
        attempt: 1,
        scopeId: "tenant-a");

    private sealed class FakeParser(bool noMatch, bool throws) : IObservationParser
    {
        public ObservationParserDescriptor Descriptor { get; } = new(
            "test.reservation-parser",
            1,
            ["imap.reservation-json"],
            ["mail.unparsed.v1"],
            ["reservation.v1"]);

        public Task<ObservationParserResult> ParseAsync(
            ObservationParserInput input,
            CancellationToken cancellationToken)
        {
            if (throws)
            {
                throw new InvalidOperationException("sensitive parser implementation detail");
            }

            if (noMatch)
            {
                return Task.FromResult(ObservationParserResult.NoMatch("test.parser.no-match"));
            }

            byte[] payload = "{\"operation\":\"upsert\",\"sourceSequence\":1}"u8.ToArray();
            return Task.FromResult(ObservationParserResult.Parsed([
                new ParsedObservation(
                    "reservation.v1",
                    "booking-42",
                    "1",
                    Now,
                    Now,
                    "application/json",
                    payload,
                    AdapterPayloadHash.ComputeSha256(payload))
            ]));
        }
    }

    private sealed class FakeTaskCommandDispatcher : ITaskCommandDispatcher
    {
        public FakeTaskCommandDispatcher(Guid attemptId)
        {
            byte[] raw = "raw-message"u8.ToArray();
            this.Start = new ObservationReprocessingStart(
                attemptId,
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                "tenant-a",
                "imap.reservation-json",
                "test.reservation-parser",
                1,
                "mail.unparsed.v1",
                "mailbox:42:7",
                "7",
                Now,
                Now,
                Guid.NewGuid(),
                AdapterPayloadHash.ComputeSha256(raw));
        }

        public ObservationReprocessingStart Start { get; }
        public List<ReceiveObservationCommand> Received { get; } = [];
        public List<RecordObservationReprocessingOutputCommand> Outputs { get; } = [];
        public List<CompleteObservationReprocessingCommand> Completions { get; } = [];
        public List<ScheduleObservationReprocessingRetryCommand> Retries { get; } = [];

        public Task<Result<TResponse>> DispatchAsync<TCommand, TResponse>(
            TaskExecutionContext context,
            TCommand command,
            CancellationToken cancellationToken)
            where TCommand : ICommand<TResponse>
        {
            object result = command switch
            {
                StartObservationReprocessingCommand => Result.Success(this.Start),
                ReceiveObservationCommand received => this.Receive(received),
                RecordObservationReprocessingOutputCommand output => this.Record(output),
                CompleteObservationReprocessingCommand completed => this.Complete(completed),
                ScheduleObservationReprocessingRetryCommand retry => this.Retry(retry),
                FailPreparedObservationReprocessingCommand => Result.Success(Unit.Value),
                CancelObservationReprocessingCommand => Result.Success(Unit.Value),
                _ => throw new InvalidOperationException($"Unexpected command {command.GetType().Name}.")
            };
            return Task.FromResult((Result<TResponse>)result);
        }

        private Result<AdapterObservationResult> Receive(ReceiveObservationCommand command)
        {
            this.Received.Add(command);
            return Result.Success(new AdapterObservationResult(
                command.OperationId,
                AdapterObservationDisposition.Accepted,
                Guid.NewGuid(),
                null));
        }

        private Result<Unit> Record(RecordObservationReprocessingOutputCommand command)
        {
            this.Outputs.Add(command);
            return Result.Success(Unit.Value);
        }

        private Result<Unit> Complete(CompleteObservationReprocessingCommand command)
        {
            this.Completions.Add(command);
            return Result.Success(Unit.Value);
        }

        private Result<Unit> Retry(ScheduleObservationReprocessingRetryCommand command)
        {
            this.Retries.Add(command);
            return Result.Success(Unit.Value);
        }
    }

    private sealed class FakeRawPayloadStore(byte[] content) : IRawPayloadStore
    {
        public Task StoreAsync(RawPayloadWrite write, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<RawPayloadRead?> ReadAsync(
            Guid payloadId,
            string scopeId,
            Guid connectionId,
            CancellationToken cancellationToken) => Task.FromResult<RawPayloadRead?>(new RawPayloadRead(
                "message/rfc822",
                content,
                AdapterPayloadHash.ComputeSha256(content)));

        public Task<bool> DeleteAsync(
            Guid payloadId,
            string scopeId,
            Guid connectionId,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}

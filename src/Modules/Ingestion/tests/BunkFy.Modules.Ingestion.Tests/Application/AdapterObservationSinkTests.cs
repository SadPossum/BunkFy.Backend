namespace BunkFy.Modules.Ingestion.Tests.Application;

using System.Text;
using BunkFy.Adapter.Abstractions;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using BunkFy.Modules.Ingestion.Application;
using BunkFy.Modules.Ingestion.Application.Adapters;
using BunkFy.Modules.Ingestion.Application.Commands;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

[Trait("Category", "Unit")]
public sealed class AdapterObservationSinkTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 12, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Matching_assignment_dispatches_receipts_then_advances_checkpoint()
    {
        FakeDispatcher dispatcher = new();
        AdapterRunAssignment assignment = CreateAssignment();
        IAdapterObservationSink sink = CreateFactory(dispatcher).Create(assignment);
        AdapterObservationSubmission submission = CreateSubmission(assignment);

        AdapterObservationAcknowledgement acknowledgement = await sink.SubmitAsync(
            submission,
            CancellationToken.None);

        Assert.All(acknowledgement.Results, result => Assert.Equal(AdapterObservationDisposition.Accepted, result.Disposition));
        Assert.True(acknowledgement.CheckpointAccepted);
        Assert.Equal("cursor-2", acknowledgement.AcceptedCheckpoint);
        Assert.Equal(2, dispatcher.Received.Count);
        Assert.Single(dispatcher.Checkpoints);
    }

    [Fact]
    public async Task Assignment_mismatch_rejects_without_dispatching_commands()
    {
        FakeDispatcher dispatcher = new();
        AdapterRunAssignment assignment = CreateAssignment();
        IAdapterObservationSink sink = CreateFactory(dispatcher).Create(assignment);
        AdapterObservationSubmission submission = new(
            assignment.RunId,
            Guid.NewGuid(),
            [CreateRecord("1")],
            "cursor-2");

        AdapterObservationAcknowledgement acknowledgement = await sink.SubmitAsync(
            submission,
            CancellationToken.None);

        Assert.Equal(AdapterObservationDisposition.Rejected, Assert.Single(acknowledgement.Results).Disposition);
        Assert.False(acknowledgement.CheckpointAccepted);
        Assert.Empty(dispatcher.Received);
        Assert.Empty(dispatcher.Checkpoints);
    }

    [Fact]
    public async Task Rejected_record_prevents_checkpoint_advancement()
    {
        FakeDispatcher dispatcher = new() { RejectRevision = "2" };
        AdapterRunAssignment assignment = CreateAssignment();
        IAdapterObservationSink sink = CreateFactory(dispatcher).Create(assignment);
        AdapterObservationSubmission submission = CreateSubmission(assignment);

        AdapterObservationAcknowledgement acknowledgement = await sink.SubmitAsync(
            submission,
            CancellationToken.None);

        Assert.Contains(acknowledgement.Results, result => result.Disposition == AdapterObservationDisposition.Rejected);
        Assert.False(acknowledgement.CheckpointAccepted);
        Assert.Empty(dispatcher.Checkpoints);
    }

    private static IAdapterObservationSinkFactory CreateFactory(FakeDispatcher dispatcher)
    {
        ServiceCollection services = new();
        services.AddSingleton<IRequestDispatcher>(dispatcher);
        services.AddIngestionApplication();
        return services.BuildServiceProvider().GetRequiredService<IAdapterObservationSinkFactory>();
    }

    private static AdapterRunAssignment CreateAssignment() => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        "tenant-a",
        Guid.NewGuid(),
        "fake.http",
        AdapterExecutionMode.Polling,
        Now,
        Now.AddMinutes(5),
        "cursor-1");

    private static AdapterObservationSubmission CreateSubmission(AdapterRunAssignment assignment) => new(
        assignment.RunId,
        assignment.LeaseId,
        [CreateRecord("1"), CreateRecord("2")],
        "cursor-2");

    private static AdapterObservedRecord CreateRecord(string revision)
    {
        byte[] payload = Encoding.UTF8.GetBytes($"{{\"revision\":{revision}}}");
        return new(
            Guid.NewGuid(),
            "reservation.changed",
            "booking-123",
            revision,
            Now.AddMinutes(-1),
            Now,
            "application/json",
            payload,
            AdapterPayloadHash.ComputeSha256(payload));
    }

    private sealed class FakeDispatcher : IRequestDispatcher
    {
        public List<ReceiveObservationCommand> Received { get; } = [];
        public List<AdvanceConnectionCheckpointCommand> Checkpoints { get; } = [];
        public string? RejectRevision { get; init; }

        public Task<Result<TResponse>> SendAsync<TResponse>(
            ICommand<TResponse> command,
            CancellationToken cancellationToken = default)
        {
            object result = command switch
            {
                ReceiveObservationCommand receive => this.Receive(receive),
                AdvanceConnectionCheckpointCommand checkpoint => this.Advance(checkpoint),
                _ => throw new InvalidOperationException($"Unexpected command {command.GetType().Name}.")
            };
            return Task.FromResult((Result<TResponse>)result);
        }

        public Task<Result<TResponse>> QueryAsync<TResponse>(
            IQuery<TResponse> query,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        private Result<AdapterObservationResult> Receive(ReceiveObservationCommand command)
        {
            this.Received.Add(command);
            return command.SourceRevision == this.RejectRevision
                ? Result.Failure<AdapterObservationResult>(IngestionApplicationErrors.ObservationInvalid)
                : Result.Success(new AdapterObservationResult(
                    command.OperationId,
                    AdapterObservationDisposition.Accepted,
                    Guid.NewGuid(),
                    null));
        }

        private Result<Unit> Advance(AdvanceConnectionCheckpointCommand command)
        {
            this.Checkpoints.Add(command);
            return Result.Success(Unit.Value);
        }
    }
}

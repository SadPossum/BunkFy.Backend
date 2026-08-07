namespace BunkFy.Modules.Reservations.Tests;

using System.Text.Json;
using BunkFy.Modules.Reservations.Application;
using BunkFy.Modules.Reservations.Application.Commands;
using BunkFy.Modules.Reservations.Application.Handlers;
using BunkFy.Modules.Reservations.Application.Tasks;
using BunkFy.Modules.Reservations.Contracts;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Tasks;
using Gma.Framework.Tasks.Cqrs;
using Xunit;

[Trait("Category", "Unit")]
public sealed class ReservationGuestRecordLinkTaskTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 6, 17, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Ready_event_enqueues_one_deduplicated_coordinate_only_task()
    {
        RecordingTaskRunStore store = new();
        ReservationGuestRecordLinkReadyHandler handler = new(
            store,
            new TestClock(),
            new TestIdGenerator());
        Guid operationId = Guid.NewGuid();
        Guid propertyId = Guid.NewGuid();
        Guid reservationId = Guid.NewGuid();

        await handler.HandleAsync(
            new ReservationGuestRecordLinkReadyIntegrationEvent(
                Guid.NewGuid(),
                "tenant-a",
                Now,
                operationId,
                propertyId,
                reservationId,
                dispatchRevision: 2),
            CancellationToken.None);

        TaskRunRequest request = Assert.IsType<TaskRunRequest>(store.Request);
        Assert.Equal(ReservationsModuleMetadata.Name, request.ModuleName);
        Assert.Equal(ExecuteReservationGuestRecordLinkPayload.TaskName, request.TaskName);
        Assert.Equal(ReservationsModuleMetadata.GuestRecordLinkWorkerGroup, request.WorkerGroup);
        Assert.Equal("tenant-a", request.ScopeId);
        Assert.Equal(reservationId, request.CorrelationId);
        Assert.Equal(ExecuteReservationGuestRecordLinkPayload.MaximumAttempts, request.MaxAttempts);
        Assert.Equal($"{operationId:N}:2", request.DeduplicationKey);
        ExecuteReservationGuestRecordLinkPayload payload =
            JsonSerializer.Deserialize<ExecuteReservationGuestRecordLinkPayload>(
                request.PayloadJson)!;
        Assert.Equal(operationId, payload.OperationId);
        Assert.Equal(propertyId, payload.PropertyId);
        Assert.Equal(reservationId, payload.ReservationId);
        Assert.Equal(2, payload.DispatchRevision);
        Assert.DoesNotContain("name", request.PayloadJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("email", request.PayloadJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("phone", request.PayloadJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("notes", request.PayloadJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Task_handler_marks_the_runtime_last_attempt_for_bounded_review()
    {
        RecordingTaskCommandDispatcher dispatcher = new();
        ExecuteReservationGuestRecordLinkTaskHandler handler = new(dispatcher);
        ExecuteReservationGuestRecordLinkPayload payload = new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            DispatchRevision: 1);

        await handler.HandleAsync(
            payload,
            Context(ExecuteReservationGuestRecordLinkPayload.MaximumAttempts),
            CancellationToken.None);

        AdvanceReservationGuestRecordLinkCommand command =
            Assert.IsType<AdvanceReservationGuestRecordLinkCommand>(
                dispatcher.Command);
        Assert.True(command.IsFinalAttempt);
        Assert.Equal(payload.OperationId, command.OperationId);
        Assert.Equal(payload.DispatchRevision, command.DispatchRevision);
    }

    [Fact]
    public async Task Missing_process_after_owner_lifecycle_cleanup_is_a_safe_no_op()
    {
        ExecuteReservationGuestRecordLinkTaskHandler handler = new(
            new MissingProcessTaskCommandDispatcher());

        await handler.HandleAsync(
            new(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                DispatchRevision: 1),
            Context(attempt: 1),
            CancellationToken.None);
    }

    private static TaskExecutionContext Context(int attempt) => new(
        Guid.NewGuid(),
        ReservationsModuleMetadata.Name,
        ExecuteReservationGuestRecordLinkPayload.TaskName,
        ReservationsModuleMetadata.GuestRecordLinkWorkerGroup,
        "worker-1",
        "node-1",
        attempt,
        "tenant-a");

    private sealed class RecordingTaskCommandDispatcher : ITaskCommandDispatcher
    {
        public object? Command { get; private set; }

        public Task<Result<TResponse>> DispatchAsync<TCommand, TResponse>(
            TaskExecutionContext context,
            TCommand command,
            CancellationToken cancellationToken)
            where TCommand : ICommand<TResponse>
        {
            this.Command = command;
            object response = new ReservationGuestRecordLinkProcessDto(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                ReservationGuestRecordLinkStatus.Completed,
                ReservationGuestRecordLinkReviewReason.None,
                Revision: 3,
                DispatchRevision: 1,
                Now,
                Now);
            return Task.FromResult(Result.Success((TResponse)response));
        }
    }

    private sealed class MissingProcessTaskCommandDispatcher
        : ITaskCommandDispatcher
    {
        public Task<Result<TResponse>> DispatchAsync<TCommand, TResponse>(
            TaskExecutionContext context,
            TCommand command,
            CancellationToken cancellationToken)
            where TCommand : ICommand<TResponse> => Task.FromResult(
            Result.Failure<TResponse>(
                ReservationsApplicationErrors
                    .GuestRecordLinkProcessNotFound));
    }

    private sealed class RecordingTaskRunStore : ITaskRunStore
    {
        public TaskRunRequest? Request { get; private set; }

        public Task<TaskRunEnqueueResult> EnqueueAsync(
            TaskRunRequest request,
            CancellationToken cancellationToken)
        {
            this.Request = request;
            return Task.FromResult(new TaskRunEnqueueResult(null!, Created: true));
        }

        public Task<TaskRunPage> ListAsync(
            TaskRunFilter filter,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<TaskRunDetails?> GetAsync(
            Guid runId,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<TaskRunStats> GetStatsAsync(
            TaskRunStatsFilter filter,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<TaskRunLease>> ClaimReadyAsync(
            TaskWorkerClaim claim,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<TaskRunMutationOutcome> MarkStartedAsync(
            TaskExecutionContext context,
            DateTimeOffset startedAtUtc,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<TaskRunMutationOutcome> MarkSucceededAsync(
            TaskExecutionContext context,
            DateTimeOffset completedAtUtc,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<TaskRunMutationOutcome> MarkCanceledAsync(
            TaskExecutionContext context,
            DateTimeOffset canceledAtUtc,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<TaskRunMutationOutcome> MarkFailedAsync(
            TaskExecutionContext context,
            string error,
            DateTimeOffset failedAtUtc,
            DateTimeOffset? retryAtUtc,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<TaskRunMutationOutcome> RequestCancellationAsync(
            Guid runId,
            string? requestedBy,
            DateTimeOffset requestedAtUtc,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<TaskRunMutationOutcome> RetryAsync(
            Guid runId,
            string? requestedBy,
            DateTimeOffset scheduledAtUtc,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<TaskRunSummary>> MarkStaleTimedOutAsync(
            DateTimeOffset nowUtc,
            TimeSpan staleAfter,
            int maxRuns,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<TaskControlMessageEnqueueOutcome> EnqueueControlMessageAsync(
            TaskControlMessage message,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<TaskRunMutationOutcome> ReportHeartbeatAsync(
            TaskExecutionContext context,
            DateTimeOffset observedAtUtc,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<TaskRunMutationOutcome> ReportProgressAsync(
            TaskExecutionContext context,
            TaskProgress progress,
            DateTimeOffset observedAtUtc,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<TaskControlMessage>> ReadPendingAsync(
            TaskExecutionContext context,
            int maxMessages,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<TaskRunMutationOutcome> MarkHandledAsync(
            TaskExecutionContext context,
            Guid messageId,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<TaskRunMutationOutcome> MarkFailedAsync(
            TaskExecutionContext context,
            Guid messageId,
            string error,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class TestClock : ISystemClock
    {
        public DateTimeOffset UtcNow => Now;
    }

    private sealed class TestIdGenerator : IIdGenerator
    {
        public Guid NewId() => Guid.NewGuid();
    }
}

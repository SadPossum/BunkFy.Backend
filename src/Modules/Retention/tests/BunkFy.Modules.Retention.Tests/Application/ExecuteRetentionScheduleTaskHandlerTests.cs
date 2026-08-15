namespace BunkFy.Modules.Retention.Tests.Application;

using BunkFy.Modules.Retention.Application.Commands;
using BunkFy.Modules.Retention.Application.Tasks;
using BunkFy.Modules.Retention.Contracts;
using BunkFy.Modules.Retention.Domain.Models;
using Gma.Framework.Cqrs;
using Gma.Framework.Observability;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Tasks;
using Gma.Framework.Tasks.Cqrs;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

[Trait("Category", "Unit")]
public sealed class ExecuteRetentionScheduleTaskHandlerTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 27, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Owner_result_is_completed_through_the_transactional_dispatcher()
    {
        FakeTaskDispatcher dispatcher = new();
        RecordingSecuritySignalRecorder securitySignals = new();
        TestContributor contributor = new(request => Task.FromResult(
            new RetentionContributionResult(
                RetentionExecutionContract.CurrentVersion,
                RetentionContributionStatus.Completed,
                ScannedCount: 3,
                AffectedCount: 2,
                RemainingCount: 0,
                "ingestion.raw-payload.completed",
                Now)));
        ExecuteRetentionScheduleTaskHandler handler = new(
            dispatcher,
            [contributor],
            new TestClock(),
            securitySignals,
            NullLogger<ExecuteRetentionScheduleTaskHandler>.Instance);
        TaskExecutionContext context = Context();

        await handler.HandleAsync(
            Payload(),
            context,
            CancellationToken.None);

        Assert.NotNull(dispatcher.Completed);
        Assert.Equal(context.RunId, dispatcher.Completed.ExecutionId);
        Assert.Equal(RetentionContributionStatus.Completed, dispatcher.Completed.Result.Status);
        Assert.Equal(2, dispatcher.Completed.Result.AffectedCount);
        Assert.Empty(securitySignals.Records);
    }

    [Fact]
    public async Task Owner_exception_is_recorded_before_the_task_is_rethrown()
    {
        FakeTaskDispatcher dispatcher = new();
        RecordingSecuritySignalRecorder securitySignals = new();
        TestContributor contributor = new(_ =>
            throw new InvalidOperationException("owner failed"));
        ExecuteRetentionScheduleTaskHandler handler = new(
            dispatcher,
            [contributor],
            new TestClock(),
            securitySignals,
            NullLogger<ExecuteRetentionScheduleTaskHandler>.Instance);
        TaskExecutionContext context = Context();

        InvalidOperationException exception =
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                handler.HandleAsync(
                    Payload(),
                    context,
                    CancellationToken.None));

        Assert.Equal("owner failed", exception.Message);
        Assert.NotNull(dispatcher.Completed);
        Assert.Equal(RetentionContributionStatus.Failed, dispatcher.Completed.Result.Status);
        Assert.Equal("retention.owner-exception", dispatcher.Completed.Result.OutcomeCode);
        SecuritySignalRecordCapture signal = Assert.Single(
            securitySignals.Records);
        Assert.Equal("retention.scheduled-execution-failed", signal.Definition.Code);
        Assert.Equal(context.CorrelationId, signal.CorrelationId);
    }

    [Fact]
    public async Task Owner_timeout_uses_the_run_id_when_task_correlation_is_missing()
    {
        FakeTaskDispatcher dispatcher = new();
        RecordingSecuritySignalRecorder securitySignals = new();
        TestContributor contributor = new(_ =>
            throw new TimeoutException("owner timed out"));
        ExecuteRetentionScheduleTaskHandler handler = new(
            dispatcher,
            [contributor],
            new TestClock(),
            securitySignals,
            NullLogger<ExecuteRetentionScheduleTaskHandler>.Instance);
        TaskExecutionContext context = Context(includeCorrelation: false);

        await Assert.ThrowsAsync<TimeoutException>(() =>
            handler.HandleAsync(
                Payload(),
                context,
                CancellationToken.None));

        SecuritySignalRecordCapture signal = Assert.Single(
            securitySignals.Records);
        Assert.Equal(
            "retention.scheduled-execution-timed-out",
            signal.Definition.Code);
        Assert.Equal(context.RunId, signal.CorrelationId);
    }

    [Fact]
    public async Task Manual_retry_uses_the_monotonic_lease_generation()
    {
        FakeTaskDispatcher dispatcher = new();
        TestContributor contributor = new(request => Task.FromResult(
            new RetentionContributionResult(
                RetentionExecutionContract.CurrentVersion,
                RetentionContributionStatus.Completed,
                ScannedCount: 0,
                AffectedCount: 0,
                RemainingCount: 0,
                "ingestion.raw-payload.completed",
                Now)));
        ExecuteRetentionScheduleTaskHandler handler = new(
            dispatcher,
            [contributor],
            new TestClock(),
            new RecordingSecuritySignalRecorder(),
            NullLogger<ExecuteRetentionScheduleTaskHandler>.Instance);
        TaskExecutionContext context = Context(
            attempt: 1,
            leaseGeneration: 4);

        await handler.HandleAsync(
            Payload(),
            context,
            CancellationToken.None);

        Assert.Equal(1, context.Attempt);
        Assert.Equal(4, dispatcher.Started?.Attempt);
        Assert.Equal(4, dispatcher.Completed?.Attempt);
    }

    private static ExecuteRetentionSchedulePayload Payload() => new(
        "ingestion",
        "raw-source-evidence",
        ExecutionPolicyVersion: 1,
        RetentionTargetScopeKind.Tenant);

    private static TaskExecutionContext Context(
        bool includeCorrelation = true,
        int attempt = 1,
        int leaseGeneration = 1) => new(
        Guid.NewGuid(),
        RetentionModuleMetadata.Name,
        ExecuteRetentionSchedulePayload.TaskName,
        RetentionModuleMetadata.WorkerGroup,
        "worker-1",
        "node-1",
        attempt,
        scopeId: "tenant-a",
        correlationId: includeCorrelation ? Guid.NewGuid() : null,
        leaseGeneration: leaseGeneration);

    private sealed class FakeTaskDispatcher : ITaskCommandDispatcher
    {
        public BeginRetentionExecutionCommand? Started { get; private set; }
        public CompleteRetentionExecutionCommand? Completed { get; private set; }

        public Task<Result<TResponse>> DispatchAsync<TCommand, TResponse>(
            TaskExecutionContext context,
            TCommand command,
            CancellationToken cancellationToken)
            where TCommand : ICommand<TResponse>
        {
            object result = command switch
            {
                BeginRetentionExecutionCommand started => this.Start(started),
                CompleteRetentionExecutionCommand completed =>
                    this.Complete(completed),
                _ => throw new InvalidOperationException(
                    $"Unexpected command {command.GetType().Name}.")
            };
            return Task.FromResult((Result<TResponse>)result);
        }

        private Result<RetentionExecutionStart> Start(
            BeginRetentionExecutionCommand command)
        {
            this.Started = command;
            return Result.Success(new RetentionExecutionStart(
                DispatchRequired: true,
                RetentionExecutionState.Running,
                new RetentionContributionRequest(
                    RetentionExecutionContract.CurrentVersion,
                    command.ExecutionId,
                    command.TenantId,
                    command.PropertyId,
                    command.OwnerKey,
                    command.DataClassKey,
                    command.ExecutionPolicyVersion,
                    command.Attempt,
                    command.StartedAtUtc,
                    command.DeadlineUtc)));
        }

        private Result<Unit> Complete(
            CompleteRetentionExecutionCommand command)
        {
            this.Completed = command;
            return Result.Success(Unit.Value);
        }
    }

    private sealed class TestContributor(
        Func<RetentionContributionRequest, Task<RetentionContributionResult>> execute)
        : IRetentionExecutionContributor
    {
        public RetentionScheduleDescriptor Schedule { get; } = new(
            "ingestion",
            "raw-source-evidence",
            RetentionTargetScopeKind.Tenant,
            executionPolicyVersion: 1,
            TimeSpan.FromHours(1),
            maxAttempts: 3,
            executionTimeout: TimeSpan.FromMinutes(15));

        public Task<RetentionContributionResult> ExecuteAsync(
            RetentionContributionRequest request,
            CancellationToken cancellationToken) =>
            execute(request);
    }

    private sealed class TestClock : ISystemClock
    {
        public DateTimeOffset UtcNow => Now;
    }

    private sealed class RecordingSecuritySignalRecorder
        : ISecuritySignalRecorder
    {
        public List<SecuritySignalRecordCapture> Records { get; } = [];

        public SecuritySignalReceipt Record(
            SecuritySignalDefinition definition,
            Guid? correlationId = null)
        {
            this.Records.Add(new(definition, correlationId));
            return new(
                correlationId?.ToString("N") ?? new string('0', 32),
                true);
        }
    }

    private sealed record SecuritySignalRecordCapture(
        SecuritySignalDefinition Definition,
        Guid? CorrelationId);
}

namespace BunkFy.Modules.Retention.Tests.Domain;

using BunkFy.Modules.Retention.Domain.Aggregates;
using BunkFy.Modules.Retention.Domain.Models;
using BunkFy.Modules.Retention.Persistence;
using Xunit;

[Trait("Category", "Unit")]
public sealed class RetentionExecutionTests
{
    private static readonly DateTimeOffset StartedAt =
        new(2026, 7, 27, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Failed_execution_can_retry_with_a_new_attempt_window()
    {
        RetentionExecution execution = Start();
        Assert.True(execution.Complete(
            RetentionExecutionState.Failed,
            attempt: 1,
            scannedCount: 0,
            affectedCount: 0,
            remainingCount: 0,
            "retention.owner-exception",
            StartedAt.AddMinutes(6),
            holdReviewDueAtUtc: null).IsSuccess);

        Assert.True(execution.BeginRetry(
            attempt: 2,
            StartedAt.AddMinutes(10),
            StartedAt.AddMinutes(15)).IsSuccess);

        Assert.Equal(2, execution.Attempt);
        Assert.Equal(RetentionExecutionState.Running, execution.State);
        Assert.Null(execution.CompletedAtUtc);
        Assert.Null(execution.OutcomeCode);
    }

    [Fact]
    public void Completed_execution_is_terminal_and_exact_completion_replays()
    {
        RetentionExecution execution = Start();
        DateTimeOffset completedAt = StartedAt.AddMinutes(2);

        Assert.True(execution.Complete(
            RetentionExecutionState.Completed,
            attempt: 1,
            scannedCount: 3,
            affectedCount: 2,
            remainingCount: 0,
            "ingestion.raw-payload.completed",
            completedAt,
            holdReviewDueAtUtc: null).IsSuccess);
        Assert.True(execution.Complete(
            RetentionExecutionState.Completed,
            attempt: 1,
            scannedCount: 3,
            affectedCount: 2,
            remainingCount: 0,
            "ingestion.raw-payload.completed",
            completedAt,
            holdReviewDueAtUtc: null).IsSuccess);
        Assert.True(execution.BeginRetry(
            attempt: 2,
            StartedAt.AddMinutes(10),
            StartedAt.AddMinutes(15)).IsFailure);
    }

    [Fact]
    public void New_schedule_state_starts_at_version_one()
    {
        RetentionExecution execution = Start();
        RetentionScheduleState state = new(
            execution,
            StartedAt.AddHours(1));

        Assert.Equal(1, state.Version);
        Assert.Equal(RetentionExecutionState.Running, state.State);
        Assert.Equal(execution.Id, state.LastExecutionId);
    }

    [Fact]
    public void New_schedule_execution_clears_the_previous_result()
    {
        RetentionExecution first = Start();
        RetentionScheduleState state = new(
            first,
            StartedAt.AddHours(1));
        Assert.True(first.Complete(
            RetentionExecutionState.Blocked,
            attempt: 1,
            scannedCount: 1,
            affectedCount: 0,
            remainingCount: 1,
            "ingestion.raw-payload.legal-hold",
            StartedAt.AddMinutes(2),
            StartedAt.AddDays(1)).IsSuccess);
        state.RecordCompleted(first);

        RetentionExecution next = RetentionExecution.Start(
            Guid.NewGuid(),
            "tenant-a",
            "ingestion",
            "raw-source-evidence",
            RetentionExecutionTargetKind.Tenant,
            propertyId: null,
            executionPolicyVersion: 1,
            attempt: 1,
            StartedAt.AddHours(2),
            StartedAt.AddHours(2).AddMinutes(5)).Value;
        state.RecordStarted(next, StartedAt.AddHours(3));

        Assert.Equal(RetentionExecutionState.Running, state.State);
        Assert.Equal(next.Id, state.LastExecutionId);
        Assert.Null(state.LastCompletedAtUtc);
        Assert.Null(state.LastScannedCount);
        Assert.Null(state.LastAffectedCount);
        Assert.Null(state.LastRemainingCount);
        Assert.Null(state.OutcomeCode);
        Assert.Null(state.HoldReviewDueAtUtc);
    }

    private static RetentionExecution Start() =>
        RetentionExecution.Start(
            Guid.NewGuid(),
            "tenant-a",
            "ingestion",
            "raw-source-evidence",
            RetentionExecutionTargetKind.Tenant,
            propertyId: null,
            executionPolicyVersion: 1,
            attempt: 1,
            StartedAt,
            StartedAt.AddMinutes(5)).Value;
}

namespace BunkFy.Modules.Ingestion.Tests.Domain;

using BunkFy.Modules.Ingestion.Domain.Retention;
using Xunit;

[Trait("Category", "Unit")]
public sealed class IngestionRetentionExecutionTests
{
    private static readonly DateTimeOffset StartedAt =
        new(2026, 7, 27, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Running_receipt_resumes_on_a_higher_attempt()
    {
        IngestionRetentionExecution execution = Start();
        Assert.True(execution.RecordAffected(4).IsSuccess);

        Assert.True(execution.BeginRetry(
            attempt: 2,
            StartedAt.AddMinutes(10),
            StartedAt.AddMinutes(20)).IsSuccess);

        Assert.Equal(2, execution.Attempt);
        Assert.Equal(4, execution.AffectedCount);
        Assert.Equal(StartedAt.AddMinutes(10), execution.StartedAtUtc);
        Assert.Equal(StartedAt.AddMinutes(20), execution.DeadlineUtc);
    }

    [Fact]
    public void Terminal_receipt_is_replayed_and_cannot_be_reopened()
    {
        IngestionRetentionExecution execution = Start();
        DateTimeOffset completedAt = StartedAt.AddMinutes(2);
        Assert.True(execution.Complete(
            IngestionRetentionExecutionState.Completed,
            remainingCount: 0,
            "ingestion.raw-payload.completed",
            completedAt,
            holdReviewDueAtUtc: null).IsSuccess);

        Assert.True(execution.Complete(
            IngestionRetentionExecutionState.Completed,
            remainingCount: 0,
            "ingestion.raw-payload.completed",
            completedAt,
            holdReviewDueAtUtc: null).IsSuccess);
        Assert.True(execution.BeginRetry(
            attempt: 2,
            StartedAt.AddMinutes(10),
            StartedAt.AddMinutes(20)).IsFailure);
    }

    private static IngestionRetentionExecution Start() =>
        IngestionRetentionExecution.Start(
            Guid.NewGuid(),
            "tenant-a",
            "raw-source-evidence",
            executionPolicyVersion: 1,
            attempt: 1,
            StartedAt,
            StartedAt.AddMinutes(5)).Value;
}

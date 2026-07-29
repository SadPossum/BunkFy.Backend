namespace BunkFy.Modules.Guests.Tests;

using BunkFy.Modules.Guests.Domain.Retention;
using Xunit;

[Trait("Category", "Unit")]
public sealed class GuestRetentionDomainTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 28, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Terminal_execution_cannot_underreport_scanned_work()
    {
        GuestRetentionExecution execution = Start();
        Assert.True(execution.RecordAffected().IsSuccess);
        Assert.True(execution.RecordAffected().IsSuccess);

        Assert.Equal(
            "Guests.RetentionExecutionResultInvalid",
            execution.Complete(
                GuestRetentionExecutionState.Completed,
                scannedCount: 1,
                remainingCount: 0,
                "guests.guest-operational.completed",
                Now.AddMinutes(1),
                holdReviewDueAtUtc: null).Error.Code);
        Assert.Equal(
            GuestRetentionExecutionState.Running,
            execution.State);

        Assert.True(execution.Complete(
            GuestRetentionExecutionState.Completed,
            scannedCount: 2,
            remainingCount: 0,
            "guests.guest-operational.completed",
            Now.AddMinutes(1),
            holdReviewDueAtUtc: null).IsSuccess);
    }

    [Fact]
    public void Retry_must_move_execution_time_forward()
    {
        GuestRetentionExecution execution = Start();

        Assert.Equal(
            "Guests.RetentionExecutionTransitionInvalid",
            execution.BeginRetry(
                attempt: 2,
                Now.AddMinutes(-1),
                Now.AddMinutes(10)).Error.Code);
        Assert.True(execution.BeginRetry(
            attempt: 2,
            Now.AddMinutes(1),
            Now.AddMinutes(11)).IsSuccess);
        Assert.Equal(2, execution.Attempt);
    }

    [Fact]
    public void Terminal_execution_rejects_non_canonical_outcome_codes()
    {
        GuestRetentionExecution execution = Start();

        Assert.Equal(
            "Guests.RetentionExecutionResultInvalid",
            execution.Complete(
                GuestRetentionExecutionState.Completed,
                scannedCount: 0,
                remainingCount: 0,
                "guests/guest-operational/completed",
                Now.AddMinutes(1),
                holdReviewDueAtUtc: null).Error.Code);
        Assert.Equal(
            GuestRetentionExecutionState.Running,
            execution.State);
    }

    [Fact]
    public void Receipt_rejects_unbounded_property_evidence()
    {
        Assert.Equal(
            "Guests.RetentionReceiptInvalid",
            GuestRetentionAnonymisationReceipt.Create(
                Guid.NewGuid(),
                "tenant-a",
                Guid.NewGuid(),
                Guid.NewGuid(),
                selectedGuestVersion: 1,
                resultingGuestVersion: 2,
                GuestRetentionAnonymisationReceipt
                    .MaximumAffectedProperties + 1,
                Now.AddDays(-1),
                new string('a', 64),
                Guid.NewGuid(),
                "system:retention",
                Now).Error.Code);
    }

    [Fact]
    public void Checkpoint_advance_is_optimistic_and_idempotent()
    {
        GuestRetentionSweepCheckpoint checkpoint =
            GuestRetentionSweepCheckpoint.Create(
                Guid.NewGuid(),
                "tenant-a",
                "guest-operational",
                Now).Value;
        Guid executionId = Guid.NewGuid();

        Assert.True(checkpoint.Advance(
            expectedAfterProjectionOrdinal: 0,
            nextAfterProjectionOrdinal: 42,
            executionId,
            Now.AddMinutes(1)).IsSuccess);
        Assert.True(checkpoint.Advance(
            expectedAfterProjectionOrdinal: 0,
            nextAfterProjectionOrdinal: 42,
            executionId,
            Now.AddMinutes(1)).IsSuccess);
        Assert.Equal(
            "Guests.RetentionCheckpointConflict",
            checkpoint.Advance(
                expectedAfterProjectionOrdinal: 0,
                nextAfterProjectionOrdinal: 43,
                executionId,
                Now.AddMinutes(1)).Error.Code);
    }

    private static GuestRetentionExecution Start() =>
        GuestRetentionExecution.Start(
            Guid.NewGuid(),
            "tenant-a",
            "guest-operational",
            executionPolicyVersion: 1,
            attempt: 1,
            startingProjectionOrdinal: 0,
            Now,
            Now.AddMinutes(15)).Value;
}

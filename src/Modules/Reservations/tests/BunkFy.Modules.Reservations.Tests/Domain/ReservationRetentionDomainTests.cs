namespace BunkFy.Modules.Reservations.Tests;

using BunkFy.Modules.Reservations.Domain.Aggregates;
using BunkFy.Modules.Reservations.Domain.DataRights;
using BunkFy.Modules.Reservations.Domain.Models;
using BunkFy.Modules.Reservations.Domain.Retention;
using Xunit;

[Trait("Category", "Unit")]
public sealed class ReservationRetentionDomainTests
{
    private static readonly DateTimeOffset Now =
        ReservationRetentionTestData.Now;

    [Fact]
    public void Terminal_execution_cannot_underreport_scanned_work()
    {
        ReservationRetentionExecution execution = Start();
        Assert.True(execution.RecordAffected().IsSuccess);
        Assert.True(execution.RecordAffected().IsSuccess);

        Assert.Equal(
            "Reservations.RetentionExecutionResultInvalid",
            execution.Complete(
                ReservationRetentionExecutionState.Completed,
                scannedCount: 1,
                remainingCount: 0,
                "reservations.reservation-operational.completed",
                Now.AddMinutes(1),
                holdReviewDueAtUtc: null).Error.Code);
        Assert.True(execution.Complete(
            ReservationRetentionExecutionState.Completed,
            scannedCount: 2,
            remainingCount: 0,
            "reservations.reservation-operational.completed",
            Now.AddMinutes(1),
            holdReviewDueAtUtc: null).IsSuccess);
    }

    [Fact]
    public void Retry_must_move_execution_time_forward()
    {
        ReservationRetentionExecution execution = Start();

        Assert.Equal(
            "Reservations.RetentionExecutionTransitionInvalid",
            execution.BeginRetry(
                attempt: 2,
                Now.AddMinutes(-1),
                Now.AddMinutes(10)).Error.Code);
        Assert.True(execution.BeginRetry(
            attempt: 2,
            Now.AddMinutes(1),
            Now.AddMinutes(11)).IsSuccess);
    }

    [Fact]
    public void Checkpoint_advance_is_optimistic_and_idempotent()
    {
        ReservationRetentionSweepCheckpoint checkpoint =
            ReservationRetentionSweepCheckpoint.Create(
                Guid.NewGuid(),
                "tenant-a",
                "reservation-operational",
                executionPolicyVersion: 1,
                Now).Value;
        Assert.Equal(1, checkpoint.ExecutionPolicyVersion);
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
            "Reservations.RetentionCheckpointConflict",
            checkpoint.Advance(
                expectedAfterProjectionOrdinal: 0,
                nextAfterProjectionOrdinal: 43,
                executionId,
                Now.AddMinutes(1)).Error.Code);
    }

    [Fact]
    public void Retention_tombstone_cannot_enter_data_rights_restore_path()
    {
        Guid propertyId = Guid.NewGuid();
        Guid reservationId = Guid.NewGuid();
        ReservationAnonymisationOutcome outcome = new(
            PreviousVersion: 3,
            CurrentVersion: 4,
            PreviousDetailsRevision: 2,
            CurrentDetailsRevision: 3,
            RemovedGuestLinkCount: 1,
            Guid.NewGuid(),
            "system:retention",
            ["PrimaryGuestName"],
            Now);
        ReservationRetentionAnonymisationReceipt receipt =
            ReservationRetentionAnonymisationReceipt.Create(
                Guid.NewGuid(),
                "tenant-a",
                Guid.NewGuid(),
                propertyId,
                reservationId,
                outcome,
                Now.AddDays(-400),
                Now.AddDays(-35),
                new string('a', 64),
                redactedHistoryCount: 1,
                reducedExternalOperationCount: 0,
                suppressedReminderCount: 0).Value;
        ReservationAnonymisationTombstone tombstone =
            ReservationAnonymisationTombstone
                .CreateForRetention(receipt).Value;

        Assert.Equal(
            ReservationAnonymisationAuthority.Retention,
            tombstone.Authority);
        Assert.True(tombstone.MatchesRetention(receipt));
        Assert.Equal(
            "Reservations.ReservationAnonymisationTombstoneInvalid",
            tombstone.AttachRestoreProof(
                propertyId,
                receipt.ContractVersion,
                receipt.Id,
                receipt.CanonicalSha256,
                receipt.ResultingReservationVersion,
                receipt.ResultingDetailsRevision,
                receipt.CompletedAtUtc,
                Guid.NewGuid(),
                Now.AddMinutes(1)).Error.Code);
    }

    [Fact]
    public void Retention_receipt_rejects_a_user_actor()
    {
        ReservationAnonymisationOutcome outcome = new(
            PreviousVersion: 3,
            CurrentVersion: 4,
            PreviousDetailsRevision: 2,
            CurrentDetailsRevision: 3,
            RemovedGuestLinkCount: 0,
            Guid.NewGuid(),
            "user:operator",
            ["PrimaryGuestName"],
            Now);

        Assert.Equal(
            "Reservations.RetentionReceiptInvalid",
            ReservationRetentionAnonymisationReceipt.Create(
                Guid.NewGuid(),
                "tenant-a",
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                outcome,
                Now.AddDays(-400),
                Now.AddDays(-35),
                new string('a', 64),
                redactedHistoryCount: 1,
                reducedExternalOperationCount: 0,
                suppressedReminderCount: 0).Error.Code);
    }

    private static ReservationRetentionExecution Start() =>
        ReservationRetentionExecution.Start(
            Guid.NewGuid(),
            "tenant-a",
            "reservation-operational",
            executionPolicyVersion: 1,
            attempt: 1,
            startingProjectionOrdinal: 0,
            Now,
            Now.AddMinutes(15)).Value;
}

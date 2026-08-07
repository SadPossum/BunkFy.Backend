namespace BunkFy.Modules.Reservations.Tests;

using BunkFy.Modules.Reservations.Domain.Events;
using BunkFy.Modules.Reservations.Domain.GuestRecords;
using Gma.Framework.Results;
using Xunit;

[Trait("Category", "Unit")]
public sealed class ReservationGuestRecordLinkProcessTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 6, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Prepare_captures_only_process_coordinates_and_normalized_actor()
    {
        Guid operationId = Guid.NewGuid();
        Guid propertyId = Guid.NewGuid();
        Guid reservationId = Guid.NewGuid();
        Guid confirmationId = Guid.NewGuid();

        ReservationGuestRecordLinkProcess process =
            ReservationGuestRecordLinkProcess.Prepare(
                operationId,
                "tenant-a",
                propertyId,
                reservationId,
                confirmationId,
                expectedReservationVersion: 7,
                " user:staff ",
                Now).Value;

        Assert.Equal(operationId, process.Id);
        Assert.Equal(propertyId, process.PropertyId);
        Assert.Equal(reservationId, process.ReservationId);
        Assert.Equal(confirmationId, process.CreationConfirmationId);
        Assert.Equal(7, process.ExpectedReservationVersion);
        Assert.Equal("user:staff", process.RequestedBy);
        Assert.Equal(ReservationGuestRecordLinkProcessState.Prepared, process.State);
        Assert.Equal(ReservationGuestRecordLinkReviewReason.None, process.ReviewReason);
        Assert.Equal(1, process.Revision);
        Assert.Equal(0, process.DispatchRevision);
        Assert.Empty(process.DomainEvents);
    }

    [Fact]
    public void Confirm_is_correlated_and_emits_one_ready_dispatch()
    {
        Guid confirmationId = Guid.NewGuid();
        Guid eventId = Guid.NewGuid();
        ReservationGuestRecordLinkProcess process = Create(confirmationId);

        Result<bool> confirmed = process.ConfirmGuest(
            confirmationId,
            eventId,
            Now.AddMinutes(1));

        Assert.True(confirmed.Value);
        Assert.Equal(ReservationGuestRecordLinkProcessState.Ready, process.State);
        Assert.Equal(2, process.Revision);
        Assert.Equal(1, process.DispatchRevision);
        ReservationGuestRecordLinkReadyDomainEvent ready =
            Assert.IsType<ReservationGuestRecordLinkReadyDomainEvent>(
                Assert.Single(process.DomainEvents));
        Assert.Equal(eventId, ready.EventId);
        Assert.Equal(process.Id, ready.OperationId);
        Assert.Equal(1, ready.DispatchRevision);

        Result<bool> replay = process.ConfirmGuest(
            confirmationId,
            Guid.NewGuid(),
            Now.AddMinutes(2));
        Assert.False(replay.Value);
        Assert.Equal(2, process.Revision);
        Assert.Single(process.DomainEvents);
    }

    [Fact]
    public void Confirm_rejects_a_different_creation_confirmation()
    {
        ReservationGuestRecordLinkProcess process = Create(Guid.NewGuid());

        Result<bool> result = process.ConfirmGuest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Now.AddMinutes(1));

        Assert.Equal(
            "Reservations.GuestRecordLinkProcessCorrelationMismatch",
            result.Error.Code);
        Assert.Equal(ReservationGuestRecordLinkProcessState.Prepared, process.State);
        Assert.Empty(process.DomainEvents);
    }

    [Fact]
    public void Review_can_be_explicitly_redispatched_and_completion_drops_transient_actor()
    {
        Guid confirmationId = Guid.NewGuid();
        ReservationGuestRecordLinkProcess process = Create(confirmationId);
        Assert.True(process.ConfirmGuest(
            confirmationId,
            Guid.NewGuid(),
            Now.AddMinutes(1)).Value);
        process.ClearDomainEvents();

        Assert.True(process.RequireReview(
            ReservationGuestRecordLinkReviewReason.GuestUnavailable,
            Now.AddMinutes(2)).Value);
        Assert.Equal(ReservationGuestRecordLinkProcessState.NeedsReview, process.State);
        Assert.Equal("user:staff", process.RequestedBy);

        Assert.True(process.Redispatch(
            Guid.NewGuid(),
            Now.AddMinutes(3)).Value);
        Assert.Equal(ReservationGuestRecordLinkProcessState.Ready, process.State);
        Assert.Equal(ReservationGuestRecordLinkReviewReason.None, process.ReviewReason);
        Assert.Equal(2, process.DispatchRevision);
        Assert.Single(process.DomainEvents);

        Assert.True(process.Complete(Now.AddMinutes(4)).Value);
        Assert.Equal(ReservationGuestRecordLinkProcessState.Completed, process.State);
        Assert.Null(process.RequestedBy);
        Assert.False(process.Complete(Now.AddMinutes(5)).Value);
    }

    [Fact]
    public void Guest_anonymisation_terminates_active_work_and_blocks_delayed_confirmation()
    {
        Guid confirmationId = Guid.NewGuid();
        ReservationGuestRecordLinkProcess process = Create(confirmationId);

        Assert.True(process.TerminateForGuestAnonymisation(
            Now.AddMinutes(1)).Value);
        Assert.Equal(
            ReservationGuestRecordLinkProcessState.NeedsReview,
            process.State);
        Assert.Equal(
            ReservationGuestRecordLinkReviewReason.GuestUnavailable,
            process.ReviewReason);

        Assert.False(process.ConfirmGuest(
            confirmationId,
            Guid.NewGuid(),
            Now.AddMinutes(2)).Value);
        Assert.Equal(
            ReservationGuestRecordLinkProcessState.NeedsReview,
            process.State);
        Assert.Empty(process.DomainEvents);
    }

    private static ReservationGuestRecordLinkProcess Create(Guid confirmationId) =>
        ReservationGuestRecordLinkProcess.Prepare(
            Guid.NewGuid(),
            "tenant-a",
            Guid.NewGuid(),
            Guid.NewGuid(),
            confirmationId,
            expectedReservationVersion: 1,
            "user:staff",
            Now).Value;
}

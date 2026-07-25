namespace BunkFy.Modules.Reservations.Tests;

using BunkFy.Modules.Reservations.Domain.DataRights;
using BunkFy.Modules.Reservations.Domain.Models;
using Gma.Framework.Results;
using Xunit;

[Trait("Category", "Unit")]
public sealed class ReservationDataHoldTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 25, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Place_normalizes_reason_and_release_is_exactly_versioned()
    {
        ReservationDataHold hold = ReservationDataHold.Place(
            Guid.NewGuid(),
            "tenant-a",
            Guid.NewGuid(),
            Guid.NewGuid(),
            " Regulatory-Request ",
            "user:privacy",
            Now).Value;

        Assert.Equal("regulatory-request", hold.ReasonCode);
        Assert.Equal(ReservationDataHoldState.Active, hold.State);
        Assert.Equal(1, hold.Version);

        Assert.True(hold.Release(
            expectedVersion: 1,
            "user:decision-maker",
            Now.AddMinutes(1)).IsSuccess);
        Assert.Equal(ReservationDataHoldState.Released, hold.State);
        Assert.Equal(2, hold.Version);
        Assert.Equal("user:decision-maker", hold.ReleasedBy);

        Result repeated = hold.Release(
            expectedVersion: 2,
            "user:decision-maker",
            Now.AddMinutes(2));
        Assert.Equal("Reservations.DataHoldAlreadyReleased", repeated.Error.Code);
        Assert.Equal(2, hold.Version);
    }

    [Fact]
    public void Receipt_binds_the_exact_transition_without_copying_actor_data()
    {
        ReservationDataHold hold = ReservationDataHold.Place(
            Guid.NewGuid(),
            "tenant-a",
            Guid.NewGuid(),
            Guid.NewGuid(),
            "dispute",
            "user:privacy",
            Now).Value;

        ReservationDataHoldReceipt placed = ReservationDataHoldReceipt.Create(
            Guid.NewGuid(),
            "tenant-a",
            Guid.NewGuid(),
            hold,
            ReservationDataHoldAction.Place,
            selectedReservationVersion: 7,
            selectedDetailsRevision: 3,
            Now).Value;

        Assert.Equal(ReservationDataHoldAction.Place, placed.Action);
        Assert.Equal(1, placed.ResultingHoldVersion);
        Assert.Equal(7, placed.SelectedReservationVersion);
        Assert.Equal(3, placed.SelectedDetailsRevision);

        Assert.True(hold.Release(
            expectedVersion: 1,
            "user:privacy",
            Now.AddMinutes(1)).IsSuccess);

        Result<ReservationDataHoldReceipt> stalePlace =
            ReservationDataHoldReceipt.Create(
                Guid.NewGuid(),
                "tenant-a",
                Guid.NewGuid(),
                hold,
                ReservationDataHoldAction.Place,
                selectedReservationVersion: 7,
                selectedDetailsRevision: 3,
                Now);
        Assert.Equal(
            "Reservations.DataHoldReceiptVersionInvalid",
            stalePlace.Error.Code);

        Result<ReservationDataHoldReceipt> wrongReleaseTime =
            ReservationDataHoldReceipt.Create(
                Guid.NewGuid(),
                "tenant-a",
                Guid.NewGuid(),
                hold,
                ReservationDataHoldAction.Release,
                selectedReservationVersion: 7,
                selectedDetailsRevision: 3,
                Now.AddMinutes(2));
        Assert.Equal(
            "Reservations.DataHoldReceiptVersionInvalid",
            wrongReleaseTime.Error.Code);
    }
}

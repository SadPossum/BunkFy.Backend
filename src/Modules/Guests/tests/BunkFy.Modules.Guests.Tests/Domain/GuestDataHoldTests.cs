namespace BunkFy.Modules.Guests.Tests;

using BunkFy.Modules.Guests.Domain.DataRights;
using BunkFy.Modules.Guests.Domain.Models;
using Gma.Framework.Results;
using Xunit;

[Trait("Category", "Unit")]
public sealed class GuestDataHoldTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 25, 1, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Place_normalizes_reason_and_release_is_versioned_and_immutable()
    {
        GuestDataHold hold = GuestDataHold.Place(
            Guid.NewGuid(),
            "tenant-a",
            Guid.NewGuid(),
            Guid.NewGuid(),
            " Regulatory-Request ",
            "user:privacy",
            Now).Value;

        Assert.Equal("regulatory-request", hold.ReasonCode);
        Assert.Equal(GuestDataHoldState.Active, hold.State);
        Assert.Equal(1, hold.Version);

        Assert.True(hold.Release(1, "user:decision-maker", Now.AddMinutes(1)).IsSuccess);
        Assert.Equal(GuestDataHoldState.Released, hold.State);
        Assert.Equal(2, hold.Version);
        Assert.Equal("user:decision-maker", hold.ReleasedBy);

        Result repeated = hold.Release(2, "user:decision-maker", Now.AddMinutes(2));
        Assert.Equal("Guests.DataHoldAlreadyReleased", repeated.Error.Code);
        Assert.Equal(2, hold.Version);
    }

    [Fact]
    public void Receipt_binds_the_exact_transition()
    {
        GuestDataHold hold = GuestDataHold.Place(
            Guid.NewGuid(),
            "tenant-a",
            Guid.NewGuid(),
            Guid.NewGuid(),
            "dispute",
            "user:privacy",
            Now).Value;

        GuestDataHoldReceipt placed = GuestDataHoldReceipt.Create(
            Guid.NewGuid(),
            "tenant-a",
            Guid.NewGuid(),
            hold,
            GuestDataHoldAction.Place,
            selectedGuestVersion: 7,
            "user:privacy",
            Now).Value;
        Assert.Equal(GuestDataHoldAction.Place, placed.Action);
        Assert.Equal(1, placed.ResultingHoldVersion);
        Assert.Equal(7, placed.SelectedGuestVersion);

        Assert.True(hold.Release(1, "user:privacy", Now.AddMinutes(1)).IsSuccess);
        Result<GuestDataHoldReceipt> invalidPlace = GuestDataHoldReceipt.Create(
            Guid.NewGuid(),
            "tenant-a",
            Guid.NewGuid(),
            hold,
            GuestDataHoldAction.Place,
            selectedGuestVersion: 7,
            "user:privacy",
            Now.AddMinutes(1));
        Assert.Equal(
            "Guests.DataHoldReceiptTransitionInvalid",
            invalidPlace.Error.Code);

        Result<GuestDataHoldReceipt> wrongActor = GuestDataHoldReceipt.Create(
            Guid.NewGuid(),
            "tenant-a",
            Guid.NewGuid(),
            hold,
            GuestDataHoldAction.Release,
            selectedGuestVersion: 7,
            "user:someone-else",
            Now.AddMinutes(1));
        Assert.Equal(
            "Guests.DataHoldReceiptTransitionInvalid",
            wrongActor.Error.Code);
    }
}

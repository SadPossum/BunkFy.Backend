namespace BunkFy.Modules.Staff.Tests.Domain;

using BunkFy.Modules.Staff.Domain.DataRights;
using BunkFy.Modules.Staff.Domain.Models;
using Gma.Framework.Results;
using Xunit;

[Trait("Category", "Unit")]
public sealed class StaffDataHoldTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 29, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Place_normalizes_reason_and_release_is_versioned()
    {
        StaffDataHold hold = StaffDataHold.Place(
            Guid.NewGuid(),
            "tenant-a",
            Guid.NewGuid(),
            " Regulatory-Request ",
            "user:privacy",
            Now).Value;

        Assert.Equal("regulatory-request", hold.ReasonCode);
        Assert.Equal(StaffDataHoldState.Active, hold.State);
        Assert.Equal(1, hold.Version);

        Assert.True(hold.Release(
            expectedVersion: 1,
            "user:decision-maker",
            Now.AddMinutes(1)).IsSuccess);
        Assert.Equal(StaffDataHoldState.Released, hold.State);
        Assert.Equal(2, hold.Version);
        Assert.Equal("user:decision-maker", hold.ReleasedBy);

        Result repeated = hold.Release(
            expectedVersion: 2,
            "user:decision-maker",
            Now.AddMinutes(2));
        Assert.Equal(
            "Staff.DataHoldAlreadyReleased",
            repeated.Error.Code);
        Assert.Equal(2, hold.Version);
    }

    [Fact]
    public void Receipts_bind_the_exact_place_and_release_transitions()
    {
        StaffDataHold hold = StaffDataHold.Place(
            Guid.NewGuid(),
            "tenant-a",
            Guid.NewGuid(),
            "dispute",
            "user:privacy",
            Now).Value;

        StaffDataHoldReceipt placed = StaffDataHoldReceipt.Create(
            Guid.NewGuid(),
            "tenant-a",
            Guid.NewGuid(),
            hold,
            StaffDataHoldAction.Place,
            selectedStaffVersion: 7,
            "user:privacy",
            Now).Value;
        Assert.Equal(StaffDataHoldAction.Place, placed.Action);
        Assert.Equal(1, placed.ResultingHoldVersion);

        Assert.True(hold.Release(
            expectedVersion: 1,
            "user:decision-maker",
            Now.AddMinutes(1)).IsSuccess);
        StaffDataHoldReceipt released = StaffDataHoldReceipt.Create(
            Guid.NewGuid(),
            "tenant-a",
            Guid.NewGuid(),
            hold,
            StaffDataHoldAction.Release,
            selectedStaffVersion: 7,
            "user:decision-maker",
            Now.AddMinutes(1)).Value;
        Assert.Equal(2, released.ResultingHoldVersion);

        Result<StaffDataHoldReceipt> wrongActor =
            StaffDataHoldReceipt.Create(
                Guid.NewGuid(),
                "tenant-a",
                Guid.NewGuid(),
                hold,
                StaffDataHoldAction.Release,
                selectedStaffVersion: 7,
                "user:someone-else",
                Now.AddMinutes(1));
        Assert.Equal(
            "Staff.DataHoldReceiptTransitionInvalid",
            wrongActor.Error.Code);
    }
}

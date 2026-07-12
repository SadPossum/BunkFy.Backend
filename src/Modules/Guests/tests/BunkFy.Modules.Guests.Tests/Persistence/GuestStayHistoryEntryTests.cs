namespace BunkFy.Modules.Guests.Tests;

using BunkFy.Modules.Guests.Contracts;
using BunkFy.Modules.Guests.Persistence;
using Xunit;

[Trait("Category", "Unit")]
public sealed class GuestStayHistoryEntryTests
{
    [Fact]
    public void Apply_is_monotonic_and_does_not_regress_on_duplicate_or_stale_delivery()
    {
        GuestStayHistoryEntry stay = new(
            "tenant-a",
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            GuestStayRole.Primary,
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 3),
            GuestStayStatus.Confirmed,
            null,
            null,
            null,
            isCurrentParticipant: true,
            reservationVersion: 3);

        stay.Apply(
            stay.PropertyId,
            GuestStayRole.Primary,
            stay.Arrival,
            stay.Departure,
            GuestStayStatus.PendingAllocation,
            null,
            null,
            null,
            isCurrentParticipant: true,
            reservationVersion: 2);
        Assert.Equal(GuestStayStatus.Confirmed, stay.Status);

        DateOnly checkIn = new(2026, 8, 1);
        stay.Apply(
            stay.PropertyId,
            GuestStayRole.Primary,
            stay.Arrival,
            stay.Departure,
            GuestStayStatus.CheckedIn,
            checkIn,
            null,
            null,
            isCurrentParticipant: true,
            reservationVersion: 4);
        Assert.Equal(GuestStayStatus.CheckedIn, stay.Status);
        Assert.Equal(checkIn, stay.CheckedInBusinessDate);
        Assert.Equal(4, stay.ReservationVersion);
    }
}

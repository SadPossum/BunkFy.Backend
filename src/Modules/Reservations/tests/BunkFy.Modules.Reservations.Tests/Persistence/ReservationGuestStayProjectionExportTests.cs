namespace BunkFy.Modules.Reservations.Tests;

using Gma.Framework.ProjectionRebuild;
using Gma.Framework.Results;
using Gma.Framework.Scoping;
using BunkFy.Modules.Guests.Contracts;
using Microsoft.EntityFrameworkCore;
using BunkFy.Modules.Reservations.Domain.Aggregates;
using BunkFy.Modules.Reservations.Persistence;
using BunkFy.Modules.Reservations.Persistence.Repositories;
using Xunit;

[Trait("Category", "Unit")]
public sealed class ReservationGuestStayProjectionExportTests
{
    [Fact]
    public async Task Export_reproduces_inactive_unlink_snapshot_and_current_stay_state()
    {
        await using ReservationsDbContext dbContext = CreateDbContext();
        DateTimeOffset now = new(2026, 7, 12, 12, 0, 0, TimeSpan.Zero);
        Guid propertyId = Guid.NewGuid();
        Guid allocationRequestId = Guid.NewGuid();
        Result<Reservation> created = Reservation.Create(
            Guid.NewGuid(),
            "tenant-a",
            propertyId,
            allocationRequestId,
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 3),
            [Guid.NewGuid()],
            "Ada Guest",
            null,
            null,
            1,
            ReservationSource.Direct,
            null,
            null,
            null,
            Guid.NewGuid(),
            Guid.NewGuid(),
            ReservationDetailsChangeOrigin.Staff,
            null,
            null,
            null,
            Guid.NewGuid(),
            now);
        Reservation reservation = created.Value;
        Guid allocationId = Guid.NewGuid();
        Assert.True(reservation.ConfirmAllocation(
            allocationRequestId,
            allocationId,
            allocationVersion: 1,
            Guid.NewGuid(),
            now).IsSuccess);
        Guid firstGuest = Guid.NewGuid();
        Guid currentGuest = Guid.NewGuid();
        Assert.True(reservation.LinkGuest(
            firstGuest,
            ReservationGuestRole.Primary,
            false,
            reservation.Version,
            "staff:one",
            Guid.NewGuid(),
            now).IsSuccess);
        Assert.True(reservation.CheckIn(
            reservation.Version,
            new DateOnly(2026, 8, 1),
            "staff:one",
            Guid.NewGuid(),
            now).IsSuccess);
        Assert.True(reservation.LinkGuest(
            currentGuest,
            ReservationGuestRole.Primary,
            true,
            reservation.Version,
            "staff:supervisor",
            Guid.NewGuid(),
            now).IsSuccess);
        long replacementVersion = reservation.Version;
        Assert.True(reservation.RequestCheckout(
            reservation.Version,
            new DateOnly(2026, 8, 3),
            "staff:supervisor",
            Guid.NewGuid(),
            Guid.NewGuid(),
            now).IsSuccess);

        dbContext.Reservations.Add(reservation);
        await dbContext.SaveChangesAsync();

        ReservationGuestStayProjectionExportSource source = new(dbContext);
        ProjectionReadBatch<ReservationGuestStayProjectionExport> batch = await source.ReadAsync(
            new ProjectionRebuildRequest("guest-stays", 1, 10),
            null,
            CancellationToken.None);

        ReservationGuestStayProjectionExport inactive = Assert.Single(batch.Snapshots, item => item.GuestId == firstGuest);
        Assert.False(inactive.IsCurrentParticipant);
        Assert.Equal(GuestStayStatus.CheckedIn, inactive.Status);
        Assert.Equal(replacementVersion, inactive.ReservationVersion);

        ReservationGuestStayProjectionExport current = Assert.Single(batch.Snapshots, item => item.GuestId == currentGuest);
        Assert.True(current.IsCurrentParticipant);
        Assert.Equal(GuestStayStatus.CheckoutPending, current.Status);
        Assert.Equal(reservation.Version, current.ReservationVersion);
    }

    private static ReservationsDbContext CreateDbContext()
    {
        DbContextOptions<ReservationsDbContext> options = new DbContextOptionsBuilder<ReservationsDbContext>()
            .UseInMemoryDatabase($"reservation-guest-export-{Guid.NewGuid():N}")
            .Options;
        return new(options, new TestScopeContext());
    }

    private sealed class TestScopeContext : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => "tenant-a";
    }
}

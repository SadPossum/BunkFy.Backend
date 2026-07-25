namespace BunkFy.Modules.Reservations.Tests.Persistence;

using BunkFy.Modules.Reservations.Application.Ports;
using BunkFy.Modules.Reservations.Domain.Aggregates;
using BunkFy.Modules.Reservations.Domain.DataRights;
using BunkFy.Modules.Reservations.Persistence;
using BunkFy.Modules.Reservations.Persistence.Repositories;
using Gma.Framework.ProjectionRebuild;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;
using Xunit;

[Trait("Category", "Unit")]
public sealed class ReservationProcessingRestrictionRebuildTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 25, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Source_derives_revision_and_active_count_from_authoritative_rows()
    {
        await using ReservationsDbContext dbContext = CreateDbContext();
        Reservation reservation = CreateReservation();
        dbContext.Reservations.Add(reservation);
        ReservationProcessingRestriction active = CreateRestriction(
            reservation,
            Now,
            applyRevision: 1);
        ReservationProcessingRestriction released = CreateRestriction(
            reservation,
            Now.AddMinutes(1),
            applyRevision: 2);
        Assert.True(released.Release(
            Guid.NewGuid(),
            releaseApprovalRevision: 3,
            reservation.Version,
            expectedVersion: 1,
            "user:privacy",
            Now.AddMinutes(2)).IsSuccess);
        dbContext.ProcessingRestrictions.AddRange(active, released);
        await dbContext.SaveChangesAsync();

        ReservationProcessingRestrictionProjectionRebuildSource source =
            new(dbContext);
        ProjectionReadBatch<ReservationProcessingRestrictionProjectionSnapshot>
            batch = await source.ReadAsync(
                new("reservation-processing-restrictions", 1, 10, false, null),
                cursor: null,
                CancellationToken.None);

        ReservationProcessingRestrictionProjectionSnapshot snapshot =
            Assert.Single(batch.Snapshots);
        Assert.Equal(3, snapshot.Revision);
        Assert.Equal(1, snapshot.ActiveRestrictionCount);
        Assert.Equal(Now.AddMinutes(2), snapshot.LastTransitionAtUtc);
        Assert.False(batch.HasMore);
    }

    private static ReservationProcessingRestriction CreateRestriction(
        Reservation reservation,
        DateTimeOffset appliedAtUtc,
        long applyRevision) =>
        ReservationProcessingRestriction.Create(
            Guid.NewGuid(),
            reservation.ScopeId,
            reservation.PropertyId,
            reservation.Id,
            Guid.NewGuid(),
            applyRevision,
            reservation.Version,
            "user:privacy",
            appliedAtUtc).Value;

    private static Reservation CreateReservation() => Reservation.Create(
        Guid.NewGuid(),
        "tenant-a",
        Guid.NewGuid(),
        Guid.NewGuid(),
        new DateOnly(2026, 8, 1),
        new DateOnly(2026, 8, 3),
        [Guid.NewGuid()],
        "Private Guest",
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
        "user:creator",
        null,
        null,
        Guid.NewGuid(),
        Now.AddDays(-1)).Value;

    private static ReservationsDbContext CreateDbContext()
    {
        DbContextOptions<ReservationsDbContext> options =
            new DbContextOptionsBuilder<ReservationsDbContext>()
                .UseInMemoryDatabase(
                    $"reservation-restriction-rebuild-{Guid.NewGuid():N}")
                .Options;
        return new(options, new TestScopeContext());
    }

    private sealed class TestScopeContext : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => "tenant-a";
    }
}

namespace BunkFy.Modules.Reservations.Tests;

using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;
using BunkFy.Modules.Reservations.Contracts;
using BunkFy.Modules.Reservations.Domain.Aggregates;
using BunkFy.Modules.Reservations.Domain.Events;
using BunkFy.Modules.Reservations.Persistence;
using BunkFy.Modules.Reservations.Persistence.Repositories;
using Xunit;

[Trait("Category", "Unit")]
public sealed class ReservationDetailsHistoryTests
{
    [Fact]
    public async Task Writer_and_reader_preserve_revision_snapshots_and_adapter_provenance()
    {
        await using ReservationsDbContext dbContext = CreateDbContext();
        ReservationDetailsHistoryWriter writer = new(dbContext);
        Guid reservationId = Guid.NewGuid();
        Guid propertyId = Guid.NewGuid();
        Guid connectionId = Guid.NewGuid();
        Guid operationId = Guid.NewGuid();
        ReservationDetailsSnapshot before = Snapshot("Ada Guest");
        ReservationDetailsSnapshot after = Snapshot("Grace Guest");
        ReservationDetailsChangedDomainEvent change = new(
            Guid.NewGuid(),
            new DateTimeOffset(2026, 7, 12, 12, 0, 0, TimeSpan.Zero),
            "tenant-a",
            reservationId,
            propertyId,
            fromRevision: 1,
            toRevision: 2,
            ReservationDetailsChangeOrigin.Adapter,
            "adapter-worker",
            connectionId,
            operationId,
            Guid.NewGuid(),
            [nameof(Reservation.PrimaryGuestName)],
            before,
            after);

        await writer.AppendAsync(change, CancellationToken.None);
        await dbContext.SaveChangesAsync();
        ReservationDetailsHistoryReader reader = new(dbContext);

        ReservationDetailsHistoryItem item = Assert.Single(await reader.ListAsync(
            propertyId,
            reservationId,
            CancellationToken.None));

        Assert.Equal(ReservationDetailsChangeOriginKind.Adapter, item.Origin);
        Assert.Equal(connectionId, item.AdapterConnectionId);
        Assert.Equal(operationId, item.ExternalOperationId);
        Assert.Equal("Ada Guest", item.Before!.PrimaryGuestName);
        Assert.Equal("Grace Guest", item.After.PrimaryGuestName);
        Assert.Equal(nameof(Reservation.PrimaryGuestName), Assert.Single(item.ChangedFields));
    }

    private static ReservationDetailsSnapshot Snapshot(string guestName) => new(
        new DateOnly(2026, 8, 1),
        new DateOnly(2026, 8, 3),
        [Guid.Parse("20000000-0000-0000-0000-000000000001")],
        guestName,
        "guest@example.test",
        null,
        1,
        null);

    private static ReservationsDbContext CreateDbContext()
    {
        DbContextOptions<ReservationsDbContext> options = new DbContextOptionsBuilder<ReservationsDbContext>()
            .UseInMemoryDatabase($"reservation-history-{Guid.NewGuid():N}")
            .Options;
        return new(options, new TestScopeContext());
    }

    private sealed class TestScopeContext : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => "tenant-a";
    }
}

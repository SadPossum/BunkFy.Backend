namespace Integration.Tests;

using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using BunkFy.Modules.Reservations.Domain.Aggregates;
using BunkFy.Modules.Reservations.Persistence;
using Testcontainers.PostgreSql;
using Xunit;

public sealed class ReservationsMigrationIntegrationTests
{
    private const string InitialMigration = "20260710200251_InitialCreate";
    private const string CanonicalGuestLinksMigration = "20260712151534_AddCanonicalGuestLinks";

    [DockerFact]
    [Trait("Category", "Docker")]
    [Trait("Category", "Integration")]
    public async Task Details_history_migration_backfills_existing_reservations()
    {
        await using PostgreSqlContainer postgreSql = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("bunkfy_reservations_history_migration_tests")
            .Build();
        await postgreSql.StartAsync();

        Guid reservationId = Guid.Parse("10000000-0000-0000-0000-000000000001");
        Guid propertyId = Guid.Parse("20000000-0000-0000-0000-000000000001");
        Guid allocationRequestId = Guid.Parse("30000000-0000-0000-0000-000000000001");
        Guid unitId = Guid.Parse("40000000-0000-0000-0000-000000000001");
        DateTimeOffset createdAtUtc = new(2026, 7, 10, 12, 0, 0, TimeSpan.Zero);

        await using (ReservationsDbContext initial = CreateDbContext(postgreSql.GetConnectionString()))
        {
            await initial.Database.GetService<IMigrator>().MigrateAsync(InitialMigration);
            await initial.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO reservations.reservations (
                    "Id", "PropertyId", "AllocationRequestId", "Arrival", "Departure",
                    "PrimaryGuestName", "Email", "GuestCount", "Source", "Status",
                    "Version", "CreatedAtUtc", "ScopeId")
                VALUES (
                    {reservationId}, {propertyId}, {allocationRequestId},
                    {new DateOnly(2026, 8, 1)}, {new DateOnly(2026, 8, 3)},
                    {"Ada Guest"}, {"ada@example.test"}, {1}, {1}, {1},
                    {1L}, {createdAtUtc}, {"tenant-a"});

                INSERT INTO reservations.requested_inventory_units ("Id", "ScopeId", "ReservationId")
                VALUES ({unitId}, {"tenant-a"}, {reservationId});
                """);
        }

        Guid guestId = Guid.Parse("50000000-0000-0000-0000-000000000001");
        await using (ReservationsDbContext canonicalLinks = CreateDbContext(postgreSql.GetConnectionString()))
        {
            await canonicalLinks.Database.GetService<IMigrator>().MigrateAsync(CanonicalGuestLinksMigration);
            await canonicalLinks.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO reservations.reservation_guests (
                    "Id", "ScopeId", "ReservationId", "Role", "LinkedBy", "LinkedAtUtc")
                VALUES (
                    {guestId}, {"tenant-a"}, {reservationId}, {1}, {"staff:migration"}, {createdAtUtc});
                """);
        }

        await using ReservationsDbContext upgraded = CreateDbContext(postgreSql.GetConnectionString());
        await upgraded.Database.MigrateAsync();

        Reservation reservation = await upgraded.Reservations
            .Include(item => item.Guests)
            .SingleAsync(item => item.Id == reservationId);
        ReservationDetailsHistoryEntry history = await upgraded.ReservationDetailsHistory.SingleAsync(
            item => item.ReservationId == reservationId);

        Assert.Equal(1, reservation.DetailsRevision);
        Assert.Equal(ReservationDetailsChangeOrigin.System, reservation.LastDetailsChangeOrigin);
        Assert.Equal(createdAtUtc, reservation.LastDetailsChangedAtUtc);
        Assert.Equal(0, history.FromRevision);
        Assert.Equal(1, history.ToRevision);
        Assert.Equal(ReservationDetailsChangeOrigin.System, history.Origin);
        Assert.Contains("Ada Guest", history.AfterSnapshotJson, StringComparison.Ordinal);
        Assert.Contains(unitId.ToString(), history.AfterSnapshotJson, StringComparison.OrdinalIgnoreCase);
        BunkFy.Modules.Reservations.Domain.Entities.ReservationGuest guest = Assert.Single(reservation.Guests);
        Assert.Equal(guestId, guest.GuestId);
        Assert.True(guest.IsCurrent);
        Assert.Equal(1, guest.LinkVersion);
        Assert.Null(guest.UnlinkedAtUtc);
    }

    private static ReservationsDbContext CreateDbContext(string connectionString)
    {
        DbContextOptions<ReservationsDbContext> options = new DbContextOptionsBuilder<ReservationsDbContext>()
            .UseNpgsql(connectionString, provider => provider
                .MigrationsAssembly(ReservationsMigrations.PostgreSqlAssembly)
                .MigrationsHistoryTable(ReservationsMigrations.HistoryTable, ReservationsMigrations.Schema))
            .Options;
        return new(options, new TestScopeContext());
    }

    private sealed class TestScopeContext : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => "tenant-a";
    }
}

namespace Integration.Tests;

using Gma.Framework.Scoping;
using Gma.Framework.Messaging.Infrastructure;
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
    private const string ArrivalRemindersMigration = "20260715123149_AddReservationArrivalReminders";

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

    [DockerFact]
    [Trait("Category", "Docker")]
    [Trait("Category", "Integration")]
    public async Task Arrival_reminder_redaction_migration_neutralizes_legacy_guest_names()
    {
        await using PostgreSqlContainer postgreSql = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("bunkfy_reservations_reminder_redaction_tests")
            .Build();
        await postgreSql.StartAsync();

        Guid eventId = Guid.Parse("60000000-0000-0000-0000-000000000001");
        DateTimeOffset occurredAtUtc = new(2026, 7, 16, 10, 30, 0, TimeSpan.Zero);
        const string eventType =
            "BunkFy.Modules.Reservations.Contracts.ReservationArrivalReminderDueIntegrationEvent";
        const string payload =
            "{\"eventId\":\"60000000-0000-0000-0000-000000000001\",\"primaryGuestName\":\"Maya Chen\"}";

        await using (ReservationsDbContext previous = CreateDbContext(postgreSql.GetConnectionString()))
        {
            await previous.Database.GetService<IMigrator>().MigrateAsync(ArrivalRemindersMigration);
            await previous.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO reservations.outbox_messages (
                    "Id", "Subject", "EventType", "Version", "ScopeId", "OccurredAtUtc",
                    "CreatedAtUtc", "Payload", "Attempts")
                VALUES (
                    {eventId}, {"gma.reservations.reservation-arrival-reminder-due.v1"},
                    {eventType}, {1}, {"tenant-a"}, {occurredAtUtc}, {occurredAtUtc}, {payload}, {0});
                """);
        }

        await using ReservationsDbContext upgraded = CreateDbContext(postgreSql.GetConnectionString());
        await upgraded.Database.MigrateAsync();

        OutboxMessage message = await upgraded.OutboxMessages.SingleAsync(item => item.Id == eventId);
        Assert.DoesNotContain("Maya Chen", message.Payload, StringComparison.Ordinal);
        Assert.Contains("A guest", message.Payload, StringComparison.Ordinal);
        Assert.Equal(1, message.Version);
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

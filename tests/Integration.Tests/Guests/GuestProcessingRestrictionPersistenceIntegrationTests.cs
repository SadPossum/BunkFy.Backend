namespace Integration.Tests;

using BunkFy.Modules.Guests.Contracts;
using BunkFy.Modules.Guests.Domain.Aggregates;
using BunkFy.Modules.Guests.Domain.DataRights;
using BunkFy.Modules.Guests.Persistence;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Testcontainers.PostgreSql;
using Xunit;

public sealed class GuestProcessingRestrictionPersistenceIntegrationTests
{
    private const string PreviousMigration =
        "20260724141436_AddGuestDataRightsCorrectionReceipts";

    [DockerFact]
    [Trait("Category", "Docker")]
    [Trait("Category", "Integration")]
    public async Task Latest_migration_initializes_every_existing_visible_guest_property()
    {
        await using PostgreSqlContainer postgreSql = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("bunkfy_guest_restriction_projection_tests")
            .Build();
        await postgreSql.StartAsync();

        Guid guestId = Guid.Parse("10000000-0000-0000-0000-000000000001");
        Guid originPropertyId = Guid.Parse("20000000-0000-0000-0000-000000000001");
        Guid stayPropertyId = Guid.Parse("20000000-0000-0000-0000-000000000002");
        DateTimeOffset createdAtUtc = new(2026, 7, 24, 16, 0, 0, TimeSpan.Zero);
        await using (GuestsDbContext initial = CreateDbContext(postgreSql.GetConnectionString()))
        {
            await initial.Database.GetService<IMigrator>().MigrateAsync(PreviousMigration);
            GuestProfile profile = GuestProfile.Create(
                guestId,
                "tenant-a",
                originPropertyId,
                "Legacy Guest",
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                "staff:migration-test",
                Guid.NewGuid(),
                createdAtUtc).Value;
            initial.GuestProfiles.Add(profile);
            initial.StayHistory.Add(new GuestStayHistoryEntry(
                "tenant-a",
                guestId,
                Guid.NewGuid(),
                stayPropertyId,
                GuestStayRole.Primary,
                new DateOnly(2026, 7, 24),
                new DateOnly(2026, 7, 25),
                GuestStayStatus.Confirmed,
                null,
                null,
                null,
                isCurrentParticipant: true,
                reservationVersion: 1));
            await initial.SaveChangesAsync();
        }

        await using (GuestsDbContext upgraded = CreateDbContext(postgreSql.GetConnectionString()))
        {
            await upgraded.Database.MigrateAsync();

            GuestProcessingRestrictionProjection[] projections =
                await upgraded.ProcessingRestrictionProjections
                    .OrderBy(projection => projection.PropertyId)
                    .ToArrayAsync();
            Assert.Equal(2, projections.Length);
            Assert.All(projections, projection =>
            {
                Assert.Equal(
                    GuestProcessingRestrictionContract.CurrentVersion,
                    projection.ContractVersion);
                Assert.False(projection.IsRestricted);
                Assert.Equal(0, projection.ActiveRestrictionCount);
                Assert.Equal(0, projection.Revision);
            });
            Assert.Contains(
                projections,
                projection => projection.PropertyId == originPropertyId);
            Assert.Contains(
                projections,
                projection => projection.PropertyId == stayPropertyId);
        }
    }

    private static GuestsDbContext CreateDbContext(string connectionString)
    {
        DbContextOptions<GuestsDbContext> options =
            new DbContextOptionsBuilder<GuestsDbContext>()
                .UseNpgsql(connectionString, provider => provider
                    .MigrationsAssembly(GuestsMigrations.PostgreSqlAssembly)
                    .MigrationsHistoryTable(
                        GuestsMigrations.HistoryTable,
                        GuestsMigrations.Schema))
                .Options;
        return new(options, new TestScopeContext());
    }

    private sealed class TestScopeContext : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => "tenant-a";
    }
}

namespace Integration.Tests;

using BunkFy.Modules.Inventory.Domain.Aggregates;
using BunkFy.Modules.Inventory.Persistence;
using Gma.Framework.Scoping;
using Integration.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

public sealed class InventoryMigrationIntegrationTests
{
    private const string PreviousMigration = "20260715064134_AddRoomRetirements";

    [DockerFact]
    [Trait("Category", "Docker")]
    [Trait("Category", "Integration")]
    public async Task Allocation_anonymisation_migration_preserves_allocations_and_backfills_operation_locks()
    {
        await using PostgreSqlContainer postgreSql = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("bunkfy_inventory_anonymisation_migration_tests")
            .Build();
        await postgreSql.StartAsync();

        Guid allocationId = Guid.Parse("10000000-0000-0000-0000-000000000001");
        Guid reservationId = Guid.Parse("20000000-0000-0000-0000-000000000001");
        Guid allocationRequestId = Guid.Parse("30000000-0000-0000-0000-000000000001");
        Guid propertyId = Guid.Parse("40000000-0000-0000-0000-000000000001");
        DateTimeOffset createdAtUtc = new(2026, 7, 26, 22, 30, 0, TimeSpan.Zero);
        const string scopeId = "tenant-a";

        await using (InventoryDbContext previous = CreateDbContext(postgreSql.GetConnectionString()))
        {
            await previous.Database.GetService<IMigrator>().MigrateAsync(PreviousMigration);
            await previous.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO inventory.allocations (
                    "Id", "ReservationId", "AllocationRequestId", "PropertyId",
                    "Arrival", "Departure", "Status", "Rejection", "Version",
                    "ReleaseRequestId", "CreatedAtUtc", "ReleasedAtUtc", "ScopeId")
                VALUES (
                    {allocationId}, {reservationId}, {allocationRequestId}, {propertyId},
                    {new DateOnly(2026, 8, 1)}, {new DateOnly(2026, 8, 3)},
                    {(int)InventoryAllocationState.Active}, {(int)InventoryAllocationRejection.None}, {1L},
                    {null}, {createdAtUtc}, {null}, {scopeId});
                """);
        }

        await using (InventoryDbContext upgraded = CreateDbContext(postgreSql.GetConnectionString()))
        {
            await upgraded.Database.MigrateAsync();

            InventoryAllocation allocation = await upgraded.Allocations
                .AsNoTracking()
                .SingleAsync(item => item.Id == allocationId);

            Assert.Equal(reservationId, allocation.ReservationId);
            Assert.Equal(allocationRequestId, allocation.AllocationRequestId);
            Assert.Equal(propertyId, allocation.PropertyId);
            Assert.Equal(InventoryAllocationState.Active, allocation.Status);
            Assert.Equal(1, allocation.Version);
            Assert.False(allocation.IsAnonymised);
            Assert.Null(allocation.AnonymisedAtUtc);
        }

        await using NpgsqlConnection connection = new(postgreSql.GetConnectionString());
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(
            """
            SELECT COUNT(*)
            FROM inventory.allocation_operation_locks
            WHERE "Id" = @allocationId
              AND "AllocationId" = @allocationId
              AND "ScopeId" = @scopeId
              AND "Revision" = 1;
            """,
            connection);
        command.Parameters.AddWithValue("allocationId", allocationId);
        command.Parameters.AddWithValue("scopeId", scopeId);

        Assert.Equal(1L, await command.ExecuteScalarAsync());
    }

    private static InventoryDbContext CreateDbContext(string connectionString)
    {
        DbContextOptions<InventoryDbContext> options =
            new DbContextOptionsBuilder<InventoryDbContext>()
                .UseNpgsql(connectionString, provider => provider
                    .MigrationsAssembly(InventoryMigrations.PostgreSqlAssembly)
                    .MigrationsHistoryTable(
                        InventoryMigrations.HistoryTable,
                        InventoryMigrations.Schema))
                .Options;
        return new(
            options,
            new TestScopeContext(),
            OpenWorkspaceTerminationFenceReader.Instance);
    }

    private sealed class TestScopeContext : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => "tenant-a";
    }
}

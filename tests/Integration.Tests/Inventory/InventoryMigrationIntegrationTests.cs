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
    private const string BeforeRetirementCancellationMigration =
        "20260809015632_AddInventoryRetirementManagementOperations";

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

    [DockerFact]
    [Trait("Category", "Docker")]
    [Trait("Category", "Integration")]
    public async Task Retirement_cancellation_migration_retains_history_and_enforces_active_attempts()
    {
        await using PostgreSqlContainer postgreSql = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("bunkfy_inventory_retirement_cancellation_migration_tests")
            .Build();
        await postgreSql.StartAsync();

        const string scopeId = "tenant-a";
        Guid propertyId = Guid.Parse("40000000-0000-0000-0000-000000000010");
        Guid bedRoomId = Guid.Parse("50000000-0000-0000-0000-000000000010");
        Guid roomRetirementRoomId = Guid.Parse("50000000-0000-0000-0000-000000000011");
        Guid bedId = Guid.Parse("60000000-0000-0000-0000-000000000010");
        Guid historicalBedRetirementId = Guid.Parse("70000000-0000-0000-0000-000000000010");
        Guid historicalRoomRetirementId = Guid.Parse("80000000-0000-0000-0000-000000000010");
        DateTimeOffset createdAtUtc = new(2026, 8, 11, 13, 0, 0, TimeSpan.Zero);

        await using (InventoryDbContext previous = CreateDbContext(postgreSql.GetConnectionString()))
        {
            await previous.Database.GetService<IMigrator>()
                .MigrateAsync(BeforeRetirementCancellationMigration);
            await previous.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO inventory.bed_retirements (
                    "Id", "PropertyId", "RoomId", "BedId", "Reason", "RequestedBy",
                    "State", "RejectionReasonCode", "Version", "CreatedAtUtc",
                    "UpdatedAtUtc", "CompletedAtUtc", "ScopeId")
                VALUES (
                    {historicalBedRetirementId}, {propertyId}, {bedRoomId}, {bedId},
                    {"Replace bed"}, {"user:manager"},
                    {(int)InventoryRetirementProcessState.Draining}, {null}, {1L},
                    {createdAtUtc}, {null}, {null}, {scopeId});

                INSERT INTO inventory.room_retirements (
                    "Id", "PropertyId", "RoomId", "Reason", "RequestedBy", "State",
                    "RejectionReasonCode", "Version", "CreatedAtUtc", "UpdatedAtUtc",
                    "CompletedAtUtc", "ScopeId")
                VALUES (
                    {historicalRoomRetirementId}, {propertyId}, {roomRetirementRoomId},
                    {"Repurpose room"}, {"user:manager"},
                    {(int)InventoryRetirementProcessState.Draining}, {null}, {1L},
                    {createdAtUtc}, {null}, {null}, {scopeId});
                """);
        }

        await using (InventoryDbContext upgraded = CreateDbContext(postgreSql.GetConnectionString()))
        {
            await upgraded.Database.MigrateAsync();

            BedRetirementProcess historicalBed = await upgraded.BedRetirements
                .SingleAsync(process => process.Id == historicalBedRetirementId);
            RoomRetirementProcess historicalRoom = await upgraded.RoomRetirements
                .SingleAsync(process => process.Id == historicalRoomRetirementId);
            InventoryPropertyTopology selectionEpoch = await upgraded
                .PropertyTopology.SingleAsync(property =>
                    property.Id == propertyId);
            DateTimeOffset canceledAtUtc = createdAtUtc.AddHours(1);
            Assert.True(historicalBed.Cancel(
                historicalBed.Version,
                "Bed repaired",
                "user:operator",
                canceledAtUtc).IsSuccess);
            Assert.True(historicalRoom.Cancel(
                historicalRoom.Version,
                "Keep room in service",
                "user:operator",
                canceledAtUtc).IsSuccess);
            selectionEpoch.AdvanceAvailabilitySelection();
            upgraded.ChangeTracker.DetectChanges();
            Assert.True(upgraded.Entry(selectionEpoch)
                .Property(property => property.AvailabilitySelectionVersion)
                .IsModified);
            await upgraded.SaveChangesAsync();

            BedRetirementProcess activeBed = BedRetirementProcess.Create(
                Guid.NewGuid(),
                scopeId,
                propertyId,
                bedRoomId,
                bedId,
                "Replace bed after season",
                "user:manager",
                canceledAtUtc.AddMinutes(1)).Value;
            BedRetirementProcess anotherHistoricalBed = BedRetirementProcess.Create(
                Guid.NewGuid(),
                scopeId,
                propertyId,
                bedRoomId,
                bedId,
                "Temporary retirement",
                "user:manager",
                canceledAtUtc.AddMinutes(2)).Value;
            Assert.True(anotherHistoricalBed.Cancel(
                anotherHistoricalBed.Version,
                "No longer needed",
                "user:operator",
                canceledAtUtc.AddMinutes(3)).IsSuccess);
            RoomRetirementProcess activeRoom = RoomRetirementProcess.Create(
                Guid.NewGuid(),
                scopeId,
                propertyId,
                roomRetirementRoomId,
                "Repurpose after season",
                "user:manager",
                canceledAtUtc.AddMinutes(1)).Value;
            upgraded.AddRange(activeBed, anotherHistoricalBed, activeRoom);
            selectionEpoch.AdvanceAvailabilitySelection();
            upgraded.ChangeTracker.DetectChanges();
            Assert.True(upgraded.Entry(selectionEpoch)
                .Property(property => property.AvailabilitySelectionVersion)
                .IsModified);
            await upgraded.SaveChangesAsync();

            Assert.Equal(3, await upgraded.BedRetirements.CountAsync(process => process.BedId == bedId));
            Assert.Equal(
                2,
                await upgraded.RoomRetirements.CountAsync(
                    process => process.RoomId == roomRetirementRoomId));
        }

        await using (InventoryDbContext duplicateBed = CreateDbContext(postgreSql.GetConnectionString()))
        {
            duplicateBed.BedRetirements.Add(BedRetirementProcess.Create(
                Guid.NewGuid(),
                scopeId,
                propertyId,
                bedRoomId,
                bedId,
                "Competing active attempt",
                "user:manager",
                createdAtUtc.AddHours(2)).Value);
            DbUpdateException failure = await Assert.ThrowsAsync<DbUpdateException>(
                () => duplicateBed.SaveChangesAsync());
            PostgresException providerFailure = Assert.IsType<PostgresException>(failure.InnerException);
            Assert.Equal(PostgresErrorCodes.UniqueViolation, providerFailure.SqlState);
            Assert.Equal(
                "UX_bed_retirements_ScopeId_BedId_active",
                providerFailure.ConstraintName);
        }

        await using (InventoryDbContext duplicateRoom = CreateDbContext(postgreSql.GetConnectionString()))
        {
            duplicateRoom.RoomRetirements.Add(RoomRetirementProcess.Create(
                Guid.NewGuid(),
                scopeId,
                propertyId,
                roomRetirementRoomId,
                "Competing active attempt",
                "user:manager",
                createdAtUtc.AddHours(2)).Value);
            DbUpdateException failure = await Assert.ThrowsAsync<DbUpdateException>(
                () => duplicateRoom.SaveChangesAsync());
            PostgresException providerFailure = Assert.IsType<PostgresException>(failure.InnerException);
            Assert.Equal(PostgresErrorCodes.UniqueViolation, providerFailure.SqlState);
            Assert.Equal(
                "UX_room_retirements_ScopeId_RoomId_active",
                providerFailure.ConstraintName);
        }

        await using (InventoryDbContext ledger = CreateDbContext(postgreSql.GetConnectionString()))
        {
            Guid operationId = Guid.NewGuid();
            await ledger.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO inventory.management_operations (
                    "Id", "ScopeId", "ResourceKind", "ResourceId", "PropertyId",
                    "Kind", "ExpectedVersion", "RequestFingerprint", "ResultVersion",
                    "CompletedAtUtc", "ResultTopologyChangeId")
                VALUES (
                    {operationId}, {scopeId}, {6}, {historicalBedRetirementId}, {propertyId},
                    {10}, {1L}, {new string('a', 64)}, {2L}, {createdAtUtc.AddHours(1)},
                    {historicalBedRetirementId});
                """);

            PostgresException invalidShape = await Assert.ThrowsAsync<PostgresException>(
                () => ledger.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO inventory.management_operations (
                        "Id", "ScopeId", "ResourceKind", "ResourceId", "PropertyId",
                        "Kind", "ExpectedVersion", "RequestFingerprint", "ResultVersion",
                        "CompletedAtUtc", "ResultTopologyChangeId")
                    VALUES (
                        {Guid.NewGuid()}, {scopeId}, {7}, {historicalBedRetirementId}, {propertyId},
                        {10}, {1L}, {new string('b', 64)}, {2L}, {createdAtUtc.AddHours(1)},
                        {historicalBedRetirementId});
                    """));
            Assert.Equal(PostgresErrorCodes.CheckViolation, invalidShape.SqlState);
            Assert.Equal(
                "CK_inventory_management_operations_kind",
                invalidShape.ConstraintName);

            PostgresException unsafeDowngrade = await Assert.ThrowsAsync<PostgresException>(
                () => ledger.Database.GetService<IMigrator>()
                    .MigrateAsync(BeforeRetirementCancellationMigration));
            Assert.Equal(PostgresErrorCodes.RaiseException, unsafeDowngrade.SqlState);
            Assert.Contains(
                "Cannot downgrade Inventory while retirement cancellation history exists",
                unsafeDowngrade.MessageText,
                StringComparison.Ordinal);
        }
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

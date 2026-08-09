namespace Integration.Tests;

using BunkFy.Modules.Inventory.Application;
using BunkFy.Modules.Inventory.Application.Commands;
using BunkFy.Modules.Inventory.Contracts;
using BunkFy.Modules.Inventory.Domain.Aggregates;
using BunkFy.Modules.Inventory.Persistence;
using BunkFy.Modules.Properties.Contracts;
using BunkFy.Modules.Workspaces.Contracts;
using Gma.Framework.Application.Events.Infrastructure;
using Gma.Framework.Cqrs;
using Gma.Framework.Cqrs.Infrastructure;
using Gma.Framework.Messaging.Infrastructure;
using Gma.Framework.Results;
using Gma.Framework.Scoping;
using Integration.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

public sealed class InventoryRoomSalesModeOperationIntegrationTests
{
    private const string PreviousMigration =
        "20260804114733_AddInventoryTenantDestructionLifecycle";
    private const string TenantId =
        "a9000000-0000-0000-0000-000000000001";
    private static readonly Guid PropertyId =
        Guid.Parse("79000000-0000-0000-0000-000000000001");
    private static readonly Guid RoomId =
        Guid.Parse("79000000-0000-0000-0000-000000000002");
    private static readonly Guid BedId =
        Guid.Parse("79000000-0000-0000-0000-000000000003");

    [DockerFact]
    [Trait("Category", "Docker")]
    [Trait("Category", "Integration")]
    public async Task Migrated_room_mode_mutations_converge_and_replay_honors_admission()
    {
        await using PostgreSqlContainer postgreSql =
            new PostgreSqlBuilder("postgres:16-alpine")
                .WithDatabase("bunkfy_inventory_room_mode_operation_tests")
                .Build();
        await postgreSql.StartAsync().ConfigureAwait(false);

        await using ServiceProvider services = CreateProvider(
            postgreSql.GetConnectionString());
        await MigrateFromPreviousAndSeedAsync(services)
            .ConfigureAwait(false);

        Guid firstOperationId = Guid.NewGuid();
        ConfigureRoomSalesModeCommand first = new(
            firstOperationId,
            PropertyId,
            RoomId,
            InventorySalesMode.BedLevel,
            ExpectedVersion: 1,
            "user:operator");
        Result<RoomInventoryMutationReceiptDto>[] concurrent =
            await Task.WhenAll(
                SendAsync(services, first),
                SendAsync(services, first)).ConfigureAwait(false);
        Assert.All(
            concurrent,
            result => Assert.True(result.IsSuccess, result.Error.Code));
        Assert.Equal(concurrent[0].Value, concurrent[1].Value);
        Assert.Equal(2, concurrent[0].Value.Version);

        Result<RoomInventoryMutationReceiptDto> conflictingReuse =
            await SendAsync(
                services,
                first with
                {
                    SalesMode = InventorySalesMode.RoomLevel,
                    ExpectedVersion = 2
                }).ConfigureAwait(false);
        Assert.Equal(
            InventoryApplicationErrors.ManagementOperationConflict,
            conflictingReuse.Error);

        ConfigureRoomSalesModeCommand later = new(
            Guid.NewGuid(),
            PropertyId,
            RoomId,
            InventorySalesMode.RoomLevel,
            ExpectedVersion: 2,
            "user:operator");
        Result<RoomInventoryMutationReceiptDto> changed = await SendAsync(
            services,
            later).ConfigureAwait(false);
        Assert.True(changed.IsSuccess, changed.Error.Code);
        Assert.Equal(3, changed.Value.Version);

        Result<RoomInventoryMutationReceiptDto> immutableReplay =
            await SendAsync(services, first).ConfigureAwait(false);
        Assert.True(immutableReplay.IsSuccess, immutableReplay.Error.Code);
        Assert.Equal(concurrent[0].Value, immutableReplay.Value);

        Guid noChangeOperationId = Guid.NewGuid();
        Result<RoomInventoryMutationReceiptDto> noChange = await SendAsync(
            services,
            later with
            {
                OperationId = noChangeOperationId,
                ExpectedVersion = 3
            }).ConfigureAwait(false);
        Assert.True(noChange.IsSuccess, noChange.Error.Code);
        Assert.Equal(3, noChange.Value.Version);

        await AssertPersistedResultsAsync(
            services,
            firstOperationId,
            later.OperationId,
            noChangeOperationId).ConfigureAwait(false);
        await AssertDatabaseConstraintAsync(services).ConfigureAwait(false);

        await CloseTenantLifecycleAsync(services).ConfigureAwait(false);
        Result<RoomInventoryMutationReceiptDto> closedReplay =
            await SendAsync(services, first).ConfigureAwait(false);
        Assert.Equal(
            InventoryApplicationErrors.WorkspaceProcessingRestricted,
            closedReplay.Error);

        await AssertDowngradeRefusedAsync(services).ConfigureAwait(false);
    }

    private static async Task MigrateFromPreviousAndSeedAsync(
        ServiceProvider services)
    {
        using IServiceScope scope = services.CreateScope();
        InventoryDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<InventoryDbContext>();
        IMigrator migrator = dbContext.Database.GetService<IMigrator>();
        await migrator.MigrateAsync(PreviousMigration).ConfigureAwait(false);

        InventoryPropertyTopology property =
            InventoryPropertyTopology.Create(PropertyId, TenantId);
        property.Apply(
            "Operation House",
            "operation-house",
            "UTC",
            PropertyStatus.Active,
            sourceVersion: 1);
        InventoryRoomTopology room = InventoryRoomTopology.Create(
            RoomId,
            TenantId,
            PropertyId);
        room.Apply(
            PropertyId,
            "101",
            "Main",
            "1",
            RoomStatus.Active,
            sourceVersion: 1);
        InventoryBedTopology bed = InventoryBedTopology.Create(
            BedId,
            TenantId,
            PropertyId,
            RoomId);
        bed.Apply(
            PropertyId,
            RoomId,
            "A",
            BedStatus.Active,
            sourceVersion: 1);
        InventoryUnit roomUnit = InventoryUnit.CreateRoom(
            RoomId,
            TenantId,
            PropertyId);
        roomUnit.Apply(
            PropertyId,
            RoomId,
            null,
            InventoryUnitKind.Room,
            "101",
            isTopologyActive: true,
            sourceVersion: 1);
        InventoryUnit bedUnit = InventoryUnit.CreateBed(
            BedId,
            TenantId,
            PropertyId,
            RoomId);
        bedUnit.Apply(
            PropertyId,
            RoomId,
            BedId,
            InventoryUnitKind.Bed,
            "A",
            isTopologyActive: true,
            sourceVersion: 1);
        RoomInventoryConfiguration configuration =
            RoomInventoryConfiguration.Create(
                RoomId,
                TenantId,
                PropertyId,
                new DateTimeOffset(
                    2026,
                    8,
                    7,
                    20,
                    0,
                    0,
                    TimeSpan.Zero)).Value;

        dbContext.AddRange(
            property,
            room,
            bed,
            roomUnit,
            bedUnit,
            configuration);
        await dbContext.SaveChangesAsync().ConfigureAwait(false);
        await migrator.MigrateAsync().ConfigureAwait(false);
    }

    private static async Task AssertPersistedResultsAsync(
        ServiceProvider services,
        Guid firstOperationId,
        Guid laterOperationId,
        Guid noChangeOperationId)
    {
        using IServiceScope scope = services.CreateScope();
        InventoryDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<InventoryDbContext>();
        RoomInventoryConfiguration configuration = await dbContext
            .RoomConfigurations
            .AsNoTracking()
            .SingleAsync(item => item.Id == RoomId)
            .ConfigureAwait(false);
        Assert.Equal(RoomSalesMode.RoomLevel, configuration.SalesMode);
        Assert.Equal(3, configuration.Version);
        Assert.Equal(3, configuration.AvailabilityMutationVersion);

        Assert.Equal(
            3,
            await dbContext.Database.SqlQuery<long>($"""
                    SELECT COUNT(*) AS "Value"
                    FROM inventory.management_operations
                    WHERE "ScopeId" = {TenantId}
                      AND "ResourceKind" = 1
                      AND "ResourceId" = {RoomId}
                      AND "Id" IN (
                          {firstOperationId},
                          {laterOperationId},
                          {noChangeOperationId})
                    """).SingleAsync().ConfigureAwait(false));
        Assert.Equal(
            2,
            await dbContext.OutboxMessages.CountAsync(message =>
                EF.Functions.Like(
                    message.EventType,
                    $"%{nameof(RoomSalesModeChangedIntegrationEvent)}%"))
                .ConfigureAwait(false));
    }

    private static async Task AssertDatabaseConstraintAsync(
        ServiceProvider services)
    {
        using IServiceScope scope = services.CreateScope();
        InventoryDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<InventoryDbContext>();
        PostgresException invalidFingerprint =
            await Assert.ThrowsAsync<PostgresException>(() =>
                dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO inventory.management_operations
                        ("Id", "ScopeId", "ResourceKind", "ResourceId",
                         "PropertyId", "Kind", "ExpectedVersion",
                         "RequestFingerprint", "ResultSalesMode",
                         "ResultVersion", "CompletedAtUtc")
                    VALUES
                        ({Guid.NewGuid()}, {TenantId}, 1, {RoomId},
                         {PropertyId}, 1, 3, {"not-a-sha256"}, 2, 3,
                         {DateTimeOffset.UtcNow})
                    """)).ConfigureAwait(false);
        Assert.Equal(PostgresErrorCodes.CheckViolation, invalidFingerprint.SqlState);
    }

    private static async Task CloseTenantLifecycleAsync(
        ServiceProvider services)
    {
        using IServiceScope scope = services.CreateScope();
        InventoryDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<InventoryDbContext>();
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE inventory.tenant_revisions
                SET "Revision" = "Revision" + 1,
                    "LifecycleStatus" = 2,
                    "DestroyOperationId" = {Guid.NewGuid()},
                    "DestroyRequestSha256" = {new string('c', 64)},
                    "DestroyStartedAtUtc" = {DateTimeOffset.UtcNow}
                WHERE "ScopeId" = {TenantId}
                """).ConfigureAwait(false);
    }

    private static async Task AssertDowngradeRefusedAsync(
        ServiceProvider services)
    {
        using IServiceScope scope = services.CreateScope();
        InventoryDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<InventoryDbContext>();
        PostgresException failure = await Assert.ThrowsAsync<PostgresException>(
            () => dbContext.Database.GetService<IMigrator>()
                .MigrateAsync(PreviousMigration)).ConfigureAwait(false);
        Assert.Contains(
            "Cannot downgrade Inventory while management operation receipts exist.",
            failure.MessageText,
            StringComparison.Ordinal);
    }

    private static async Task<Result<RoomInventoryMutationReceiptDto>>
        SendAsync(
            ServiceProvider services,
            ConfigureRoomSalesModeCommand command)
    {
        using IServiceScope scope = services.CreateScope();
        return await scope.ServiceProvider
            .GetRequiredService<IRequestDispatcher>()
            .SendAsync(command, CancellationToken.None)
            .ConfigureAwait(false);
    }

    private static ServiceProvider CreateProvider(string connectionString)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration["Persistence:Provider"] = "PostgreSql";
        builder.Configuration["ConnectionStrings:PostgreSql"] =
            connectionString;
        builder.Services.AddSingleton<IScopeContext>(new TestScopeContext());
        builder.Services.AddSingleton<IWorkspaceTerminationFenceReader>(
            OpenWorkspaceTerminationFenceReader.Instance);
        builder.AddApplicationEventsInfrastructure();
        builder.AddCqrsInfrastructure();
        builder.AddMessagingInfrastructure();
        builder.Services.AddInventoryApplication();
        builder.AddInventoryPersistence();
        return builder.Services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true });
    }

    private sealed class TestScopeContext : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => TenantId;
    }
}

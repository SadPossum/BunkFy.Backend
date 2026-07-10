namespace Integration.Tests;

using System.Globalization;
using Properties.Contracts;
using Properties.Domain.Aggregates;
using Properties.Domain.Entities;
using Properties.Persistence;
using Gma.Framework.Scoping;
using Gma.Framework.ProjectionRebuild;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;
using Xunit;

public sealed class PropertiesPersistenceIntegrationTests
{
    private const string InitialMigration = "20260709104355_InitialCreate";
    private static readonly Guid PropertyId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid RoomId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid BedId = Guid.Parse("30000000-0000-0000-0000-000000000001");

    [DockerFact]
    [Trait("Category", "Docker")]
    [Trait("Category", "Integration")]
    public async Task Scope_and_lifecycle_migration_preserves_topology_and_enforces_concurrency()
    {
        await using PostgreSqlContainer postgreSql = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("bunkfy_properties_migration_tests")
            .Build();
        await postgreSql.StartAsync();

        await using (PropertiesDbContext initial = CreateDbContext(postgreSql.GetConnectionString()))
        {
            IMigrator migrator = initial.GetService<IMigrator>();
            await migrator.MigrateAsync(InitialMigration);
            await SeedInitialSchemaAsync(initial);
            await migrator.MigrateAsync();
        }

        await using (PropertiesDbContext verification = CreateDbContext(postgreSql.GetConnectionString()))
        {
            Property property = await verification.Properties.SingleAsync(item => item.Id == PropertyId);
            Room room = await verification.Rooms.Include(item => item.Beds).SingleAsync(item => item.Id == RoomId);
            Bed bed = Assert.Single(room.Beds);

            Assert.Equal("tenant-a", property.ScopeId);
            Assert.Equal("tenant-a", room.ScopeId);
            Assert.Equal("tenant-a", bed.ScopeId);
            Assert.Equal(1, property.Version);
            Assert.Equal(1, room.Version);
            Assert.Equal(1, bed.Version);
            Assert.True(property.ProjectionOrdinal > 0);

            Property secondProperty = CreateProperty("aaa-property", "AAA Property");
            Property thirdProperty = CreateProperty("zzz-property", "ZZZ Property");
            verification.Properties.AddRange(secondProperty, thirdProperty);
            await verification.SaveChangesAsync();

            Property lowerOrdinal = secondProperty.ProjectionOrdinal < thirdProperty.ProjectionOrdinal
                ? secondProperty
                : thirdProperty;
            Property higherOrdinal = ReferenceEquals(lowerOrdinal, secondProperty) ? thirdProperty : secondProperty;
            Assert.True(lowerOrdinal.Update(
                lowerOrdinal.Name.Value,
                "zzz-cursor",
                lowerOrdinal.TimeZoneId.Value,
                lowerOrdinal.Version,
                Guid.NewGuid(),
                DateTimeOffset.UtcNow).IsSuccess);
            Assert.True(higherOrdinal.Update(
                higherOrdinal.Name.Value,
                "aaa-cursor",
                higherOrdinal.TimeZoneId.Value,
                higherOrdinal.Version,
                Guid.NewGuid(),
                DateTimeOffset.UtcNow).IsSuccess);
            await verification.SaveChangesAsync();
        }

        using (ServiceProvider provider = CreatePersistenceProvider(postgreSql.GetConnectionString()))
        using (IServiceScope scope = provider.CreateScope())
        {
            IPropertiesTopologyProjectionExportSource source =
                scope.ServiceProvider.GetRequiredService<IPropertiesTopologyProjectionExportSource>();
            ProjectionRebuildRequest request = new("inventory-topology", projectionVersion: 1, batchSize: 1);
            ProjectionReadBatch<PropertyTopologyProjectionExport> firstBatch =
                await source.ReadAsync(request, cursor: null, CancellationToken.None);
            ProjectionReadBatch<PropertyTopologyProjectionExport> secondBatch =
                await source.ReadAsync(request, firstBatch.NextCursor, CancellationToken.None);

            Assert.Equal(PropertyId, Assert.Single(firstBatch.Snapshots).PropertyId);
            Assert.Equal("zzz-cursor", Assert.Single(secondBatch.Snapshots).Code);
            Assert.True(
                long.TryParse(
                    firstBatch.NextCursor,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out long ordinal) && ordinal > 0);
        }

        await using PropertiesDbContext first = CreateDbContext(postgreSql.GetConnectionString());
        await using PropertiesDbContext second = CreateDbContext(postgreSql.GetConnectionString());
        Room firstRoom = await first.Rooms.Include(item => item.Beds).SingleAsync(item => item.Id == RoomId);
        Room staleRoom = await second.Rooms.Include(item => item.Beds).SingleAsync(item => item.Id == RoomId);

        Assert.True(firstRoom.Update("101-A", null, null, firstRoom.Version, Guid.NewGuid(), DateTimeOffset.UtcNow).IsSuccess);
        await first.SaveChangesAsync();
        Assert.True(staleRoom.Update("101-B", null, null, staleRoom.Version, Guid.NewGuid(), DateTimeOffset.UtcNow).IsSuccess);

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
    }

    private static async Task SeedInitialSchemaAsync(PropertiesDbContext dbContext)
    {
        DateTimeOffset createdAtUtc = new(2026, 7, 9, 12, 0, 0, TimeSpan.Zero);
        const string tenantId = "tenant-a";
        const string propertyName = "Hostel One";
        const string propertyCode = "hostel-one";
        const string timeZoneId = "UTC";
        const string roomName = "101";
        const string bedLabel = "A";
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO properties.properties
                ("Id", "Name", "Code", "TimeZoneId", "Status", "CreatedAtUtc", "TenantId")
            VALUES
                ({PropertyId}, {propertyName}, {propertyCode}, {timeZoneId}, {1}, {createdAtUtc}, {tenantId});

            INSERT INTO properties.rooms
                ("Id", "PropertyId", "Name", "Status", "CreatedAtUtc", "TenantId")
            VALUES
                ({RoomId}, {PropertyId}, {roomName}, {1}, {createdAtUtc}, {tenantId});

            INSERT INTO properties.beds
                ("Id", "TenantId", "PropertyId", "RoomId", "Label", "Status", "CreatedAtUtc")
            VALUES
                ({BedId}, {tenantId}, {PropertyId}, {RoomId}, {bedLabel}, {1}, {createdAtUtc});
            """);
    }

    private static PropertiesDbContext CreateDbContext(string connectionString)
    {
        DbContextOptions<PropertiesDbContext> options = new DbContextOptionsBuilder<PropertiesDbContext>()
            .UseNpgsql(
                connectionString,
                postgreSql => postgreSql
                    .MigrationsAssembly(PropertiesMigrations.PostgreSqlAssembly)
                    .MigrationsHistoryTable(PropertiesMigrations.HistoryTable, PropertiesMigrations.Schema))
            .Options;

        return new PropertiesDbContext(options, new TestScopeContext());
    }

    private static ServiceProvider CreatePersistenceProvider(string connectionString)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration["Persistence:Provider"] = "PostgreSql";
        builder.Configuration["ConnectionStrings:PostgreSql"] = connectionString;
        builder.Services.AddSingleton<IScopeContext>(new TestScopeContext());
        builder.AddPropertiesPersistence();
        return builder.Services.BuildServiceProvider();
    }

    private static Property CreateProperty(string code, string name) =>
        Property.Create(
            Guid.NewGuid(),
            "tenant-a",
            name,
            code,
            "UTC",
            Guid.NewGuid(),
            DateTimeOffset.UtcNow).Value;

    private sealed class TestScopeContext : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => "tenant-a";
    }
}

namespace BunkFy.Modules.DataRights.Tests.Persistence;

using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Persistence;
using BunkFy.Modules.DataRights.Persistence.Repositories;
using BunkFy.Modules.Properties.Contracts;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;

[Trait("Category", "Unit")]
public sealed class DataRightsPropertyProjectionRepositoryTests
{
    [Fact]
    public async Task Topology_projection_preserves_time_zone_and_ignores_stale_updates()
    {
        Guid propertyId = Guid.NewGuid();
        InMemoryDatabaseRoot root = new();
        await using DataRightsDbContext dbContext = CreateDbContext(root);
        DataRightsPropertyProjectionRepository repository = new(dbContext);

        await repository.ApplyTopologyAsync(
            new(
                "tenant-a",
                propertyId,
                "Hostel",
                "Europe/London",
                PropertyStatus.Active,
                5),
            CancellationToken.None);
        await dbContext.SaveChangesAsync();
        await repository.ApplyTopologyAsync(
            new(
                "tenant-a",
                propertyId,
                "Stale",
                "UTC",
                PropertyStatus.Retired,
                4),
            CancellationToken.None);
        await dbContext.SaveChangesAsync();

        DataRightsPropertyPolicySnapshot snapshot = Assert.IsType<
            DataRightsPropertyPolicySnapshot>(
                await repository.GetPolicyAsync(propertyId, CancellationToken.None));
        Assert.True(snapshot.IsKnown);
        Assert.Equal(PropertyStatus.Active, snapshot.Status);
        Assert.Equal("Europe/London", snapshot.TimeZoneId);
        Assert.Equal(5, snapshot.TopologySourceVersion);
        Assert.Equal(0, snapshot.PolicySourceVersion);
    }

    [Fact]
    public async Task Retire_event_without_topology_payload_preserves_projected_time_zone()
    {
        Guid propertyId = Guid.NewGuid();
        InMemoryDatabaseRoot root = new();
        await using DataRightsDbContext dbContext = CreateDbContext(root);
        DataRightsPropertyProjectionRepository repository = new(dbContext);
        await repository.ApplyTopologyAsync(
            new(
                "tenant-a",
                propertyId,
                "Hostel",
                "Europe/London",
                PropertyStatus.Active,
                5),
            CancellationToken.None);
        await repository.ApplyTopologyAsync(
            new(
                "tenant-a",
                propertyId,
                string.Empty,
                null,
                PropertyStatus.Retired,
                6),
            CancellationToken.None);
        await dbContext.SaveChangesAsync();

        DataRightsPropertyPolicySnapshot snapshot = Assert.IsType<
            DataRightsPropertyPolicySnapshot>(
                await repository.GetPolicyAsync(propertyId, CancellationToken.None));
        Assert.Equal(PropertyStatus.Retired, snapshot.Status);
        Assert.Equal("Europe/London", snapshot.TimeZoneId);
        Assert.Equal(6, snapshot.TopologySourceVersion);
    }

    private static DataRightsDbContext CreateDbContext(
        InMemoryDatabaseRoot root)
    {
        DbContextOptions<DataRightsDbContext> options =
            new DbContextOptionsBuilder<DataRightsDbContext>()
                .UseInMemoryDatabase($"data-rights-property-{Guid.NewGuid():N}", root)
                .Options;
        return new(options, new TestScopeContext());
    }

    private sealed class TestScopeContext : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => "tenant-a";
    }
}

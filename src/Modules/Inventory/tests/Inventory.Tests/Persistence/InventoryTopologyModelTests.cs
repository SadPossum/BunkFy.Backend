namespace Inventory.Tests;

using Gma.Framework.Scoping;
using Inventory.Domain.Aggregates;
using Inventory.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Properties.Contracts;
using Xunit;

[Trait("Category", "Unit")]
public sealed class InventoryTopologyModelTests
{
    [Fact]
    public void Delayed_room_event_can_authoritatively_fill_a_bed_created_placeholder()
    {
        Guid propertyId = Guid.NewGuid();
        InventoryRoomTopology room = InventoryRoomTopology.Create(Guid.NewGuid(), "tenant-a", propertyId);

        room.Apply(propertyId, "101", "Main", "1", RoomStatus.Active, 1);

        Assert.Equal(1, room.SourceVersion);
        Assert.Equal(1, room.DetailsVersion);
        Assert.Equal("101", room.Name);
        Assert.True(room.IsKnown);
        Assert.Equal(RoomStatus.Active, room.Status);
    }

    [Fact]
    public void Stale_room_event_cannot_replace_newer_details_or_status()
    {
        Guid propertyId = Guid.NewGuid();
        InventoryRoomTopology room = InventoryRoomTopology.Create(Guid.NewGuid(), "tenant-a", propertyId);
        room.Apply(propertyId, "New", null, null, RoomStatus.Retired, 3);

        room.Apply(propertyId, "Old", null, null, RoomStatus.Active, 2);

        Assert.Equal("New", room.Name);
        Assert.Equal(RoomStatus.Retired, room.Status);
        Assert.Equal(3, room.SourceVersion);
        Assert.Equal(3, room.DetailsVersion);
    }

    [Fact]
    public void Room_configuration_version_is_a_concurrency_token()
    {
        using InventoryDbContext dbContext = CreateDbContext();

        IEntityType entity = dbContext.Model.FindEntityType(typeof(RoomInventoryConfiguration))!;

        Assert.True(entity.FindProperty(nameof(RoomInventoryConfiguration.Version))!.IsConcurrencyToken);
        Assert.Contains(
            entity.GetIndexes(),
            index => index.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(RoomInventoryConfiguration.ScopeId), nameof(RoomInventoryConfiguration.PropertyId)]));
    }

    [Fact]
    public void Manual_block_has_version_concurrency_and_scope_aware_unit_foreign_key()
    {
        using InventoryDbContext dbContext = CreateDbContext();

        IEntityType blockEntity = dbContext.Model.FindEntityType(typeof(ManualInventoryBlock))!;
        IEntityType unitEntity = dbContext.Model.FindEntityType(typeof(InventoryUnit))!;
        IForeignKey foreignKey = Assert.Single(
            blockEntity.GetForeignKeys(),
            candidate => candidate.PrincipalEntityType == unitEntity);

        Assert.True(blockEntity.FindProperty(nameof(ManualInventoryBlock.Version))!.IsConcurrencyToken);
        Assert.True(unitEntity.FindProperty(nameof(InventoryUnit.AvailabilityMutationVersion))!.IsConcurrencyToken);
        Assert.Equal(["ScopeId", "InventoryUnitId"], foreignKey.Properties.Select(property => property.Name));
        Assert.Equal(["ScopeId", "Id"], foreignKey.PrincipalKey.Properties.Select(property => property.Name));
        Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior);
    }

    [Fact]
    public void Property_projection_cursor_is_generated_and_unique()
    {
        using InventoryDbContext dbContext = CreateDbContext();

        IEntityType propertyEntity = dbContext.Model.FindEntityType(typeof(InventoryPropertyTopology))!;
        IProperty cursor = propertyEntity.FindProperty(nameof(InventoryPropertyTopology.ProjectionOrdinal))!;

        Assert.Equal(ValueGenerated.OnAdd, cursor.ValueGenerated);
        Assert.Contains(
            propertyEntity.GetIndexes(),
            index => index.IsUnique && index.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(InventoryPropertyTopology.ProjectionOrdinal)]));
    }

    [Fact]
    public void Allocation_model_has_idempotency_indexes_scoped_units_and_version_concurrency()
    {
        using InventoryDbContext dbContext = CreateDbContext();

        IEntityType allocationEntity = dbContext.Model.FindEntityType(typeof(InventoryAllocation))!;
        IEntityType allocationUnitEntity = dbContext.Model.FindEntityType(typeof(Inventory.Domain.Entities.InventoryAllocationUnit))!;
        IForeignKey parent = Assert.Single(
            allocationUnitEntity.GetForeignKeys(),
            candidate => candidate.PrincipalEntityType == allocationEntity);

        Assert.True(allocationEntity.FindProperty(nameof(InventoryAllocation.Version))!.IsConcurrencyToken);
        Assert.Contains(
            allocationEntity.GetIndexes(),
            index => index.IsUnique && index.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(InventoryAllocation.ScopeId), nameof(InventoryAllocation.AllocationRequestId)]));
        Assert.Contains(
            allocationEntity.GetIndexes(),
            index => index.IsUnique && index.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(InventoryAllocation.ScopeId), nameof(InventoryAllocation.ReservationId)]));
        Assert.Equal(["ScopeId", "AllocationId"], parent.Properties.Select(property => property.Name));
        Assert.Equal(["ScopeId", "Id"], parent.PrincipalKey.Properties.Select(property => property.Name));
        Assert.Equal(DeleteBehavior.Cascade, parent.DeleteBehavior);
    }

    private static InventoryDbContext CreateDbContext()
    {
        DbContextOptions<InventoryDbContext> options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseInMemoryDatabase($"inventory-model-{Guid.NewGuid():N}")
            .Options;

        return new InventoryDbContext(options, new TestScopeContext());
    }

    private sealed class TestScopeContext : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => "tenant-a";
    }
}

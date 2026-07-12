namespace BunkFy.Modules.Reservations.Tests;

using BunkFy.Modules.Inventory.Contracts;
using BunkFy.Modules.Reservations.Application.Ports;
using BunkFy.Modules.Reservations.Persistence;
using Xunit;

[Trait("Category", "Unit")]
public sealed class InventoryProjectionStateTests
{
    [Fact]
    public void Stale_unit_definition_cannot_replace_newer_snapshot()
    {
        Guid unitId = Guid.NewGuid();
        ReservationInventoryUnitProjection projection = ReservationInventoryUnitProjection.Create(Unit(unitId, "current", 5));

        projection.Apply(Unit(unitId, "stale", 4));

        Assert.Equal("current", projection.Label);
        Assert.Equal(5, projection.UnitVersion);
    }

    [Fact]
    public void Released_block_tombstone_ignores_older_create_but_rebuild_can_hydrate_same_version()
    {
        Guid blockId = Guid.NewGuid();
        Guid propertyId = Guid.NewGuid();
        Guid unitId = Guid.NewGuid();
        ReservationInventoryBlockProjection projection = ReservationInventoryBlockProjection.CreateReleasedTombstone(
            "tenant-a",
            blockId,
            propertyId,
            unitId,
            version: 2);

        projection.Apply(Block(blockId, propertyId, unitId, ManualInventoryBlockStatus.Active, version: 1));
        Assert.Equal(ManualInventoryBlockStatus.Released, projection.Status);
        Assert.False(projection.IsKnown);

        projection.Apply(Block(blockId, propertyId, unitId, ManualInventoryBlockStatus.Released, version: 2));
        Assert.Equal(ManualInventoryBlockStatus.Released, projection.Status);
        Assert.True(projection.IsKnown);
        Assert.NotNull(projection.Arrival);
    }

    [Fact]
    public void Released_allocation_tombstone_ignores_older_confirmation_but_rebuild_can_hydrate_same_version()
    {
        Guid allocationId = Guid.NewGuid();
        Guid reservationId = Guid.NewGuid();
        Guid propertyId = Guid.NewGuid();
        Guid unitId = Guid.NewGuid();
        ReservationInventoryAllocationProjection projection = ReservationInventoryAllocationProjection.CreateReleasedTombstone(
            "tenant-a",
            allocationId,
            reservationId,
            version: 2);

        projection.Apply(Allocation(
            allocationId,
            reservationId,
            propertyId,
            unitId,
            InventoryAllocationStatus.Active,
            version: 1));
        Assert.Equal(InventoryAllocationStatus.Released, projection.Status);
        Assert.False(projection.IsKnown);

        projection.Apply(Allocation(
            allocationId,
            reservationId,
            propertyId,
            unitId,
            InventoryAllocationStatus.Released,
            version: 2));
        Assert.Equal(InventoryAllocationStatus.Released, projection.Status);
        Assert.True(projection.IsKnown);
        Assert.Single(projection.Units);
    }

    private static ReservationInventoryUnitWriteModel Unit(Guid unitId, string label, long version) => new(
        "tenant-a",
        unitId,
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
        Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
        null,
        InventoryUnitKind.Room,
        label,
        true,
        true,
        2,
        version);

    private static ReservationInventoryBlockWriteModel Block(
        Guid blockId,
        Guid propertyId,
        Guid unitId,
        ManualInventoryBlockStatus status,
        long version) => new(
            "tenant-a",
            blockId,
            propertyId,
            unitId,
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 3),
            status,
            version);

    private static ReservationInventoryAllocationWriteModel Allocation(
        Guid allocationId,
        Guid reservationId,
        Guid propertyId,
        Guid unitId,
        InventoryAllocationStatus status,
        long version) => new(
            "tenant-a",
            allocationId,
            reservationId,
            propertyId,
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 3),
            status,
            [unitId],
            version);
}

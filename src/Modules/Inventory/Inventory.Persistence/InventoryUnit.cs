namespace Inventory.Persistence;

using Gma.Framework.Domain.Models;
using Inventory.Contracts;

public sealed class InventoryUnit : ScopedEntity<Guid>
{
    private InventoryUnit() { }

    private InventoryUnit(
        Guid inventoryUnitId,
        string scopeId,
        Guid propertyId,
        Guid roomId,
        Guid? bedId,
        InventoryUnitKind kind)
        : base(inventoryUnitId, scopeId)
    {
        this.PropertyId = propertyId;
        this.RoomId = roomId;
        this.BedId = bedId;
        this.Kind = kind;
    }

    public Guid PropertyId { get; private set; }
    public Guid RoomId { get; private set; }
    public Guid? BedId { get; private set; }
    public InventoryUnitKind Kind { get; private set; }
    public string Label { get; private set; } = string.Empty;
    public bool IsTopologyActive { get; private set; }
    public long SourceVersion { get; private set; }
    public long DetailsVersion { get; private set; }
    public bool IsKnown { get; private set; }
    public long AvailabilityMutationVersion { get; private set; } = 1;

    public static InventoryUnit CreateRoom(Guid roomId, string scopeId, Guid propertyId) =>
        new(roomId, scopeId, propertyId, roomId, null, InventoryUnitKind.Room);

    public static InventoryUnit CreateBed(Guid bedId, string scopeId, Guid propertyId, Guid roomId) =>
        new(bedId, scopeId, propertyId, roomId, bedId, InventoryUnitKind.Bed);

    public void Apply(
        Guid propertyId,
        Guid roomId,
        Guid? bedId,
        InventoryUnitKind kind,
        string? label,
        bool isTopologyActive,
        long sourceVersion)
    {
        if (this.PropertyId != propertyId || this.RoomId != roomId || this.BedId != bedId || this.Kind != kind)
        {
            throw new InvalidOperationException("An inventory unit identity cannot move to different topology.");
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sourceVersion);
        if (sourceVersion > this.SourceVersion)
        {
            this.SourceVersion = sourceVersion;
            this.IsTopologyActive = isTopologyActive;
        }

        if (label is not null && sourceVersion > this.DetailsVersion)
        {
            this.Label = label;
            this.DetailsVersion = sourceVersion;
            this.IsKnown = true;
        }
    }

    public void TouchAvailability() => this.AvailabilityMutationVersion++;
}

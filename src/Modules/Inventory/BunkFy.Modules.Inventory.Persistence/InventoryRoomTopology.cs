namespace BunkFy.Modules.Inventory.Persistence;

using Gma.Framework.Domain.Models;
using BunkFy.Modules.Properties.Contracts;

public sealed class InventoryRoomTopology : ScopedEntity<Guid>
{
    private InventoryRoomTopology() { }

    private InventoryRoomTopology(Guid roomId, string scopeId, Guid propertyId)
        : base(roomId, scopeId)
        => this.PropertyId = propertyId;

    public Guid PropertyId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? BuildingLabel { get; private set; }
    public string? FloorLabel { get; private set; }
    public RoomStatus Status { get; private set; } = RoomStatus.Unknown;
    public long SourceVersion { get; private set; }
    public long DetailsVersion { get; private set; }
    public bool IsKnown { get; private set; }

    public static InventoryRoomTopology Create(Guid roomId, string scopeId, Guid propertyId) =>
        new(roomId, scopeId, propertyId);

    public void Apply(
        Guid propertyId,
        string? name,
        string? buildingLabel,
        string? floorLabel,
        RoomStatus status,
        long sourceVersion)
    {
        if (this.PropertyId != propertyId)
        {
            throw new InvalidOperationException("A room topology id cannot move to another property.");
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sourceVersion);

        if (sourceVersion > this.SourceVersion)
        {
            this.SourceVersion = sourceVersion;
            this.Status = status;
        }

        if (name is not null && sourceVersion > this.DetailsVersion)
        {
            this.Name = name;
            this.BuildingLabel = buildingLabel;
            this.FloorLabel = floorLabel;
            this.DetailsVersion = sourceVersion;
            this.IsKnown = true;
        }
    }

}

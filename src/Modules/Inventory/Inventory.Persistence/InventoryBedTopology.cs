namespace Inventory.Persistence;

using Gma.Framework.Domain.Models;
using Properties.Contracts;

public sealed class InventoryBedTopology : ScopedEntity<Guid>
{
    private InventoryBedTopology() { }

    private InventoryBedTopology(Guid bedId, string scopeId, Guid propertyId, Guid roomId)
        : base(bedId, scopeId)
    {
        this.PropertyId = propertyId;
        this.RoomId = roomId;
    }

    public Guid PropertyId { get; private set; }
    public Guid RoomId { get; private set; }
    public string Label { get; private set; } = string.Empty;
    public BedStatus Status { get; private set; } = BedStatus.Unknown;
    public long SourceVersion { get; private set; }
    public long DetailsVersion { get; private set; }
    public bool IsKnown { get; private set; }

    public static InventoryBedTopology Create(Guid bedId, string scopeId, Guid propertyId, Guid roomId) =>
        new(bedId, scopeId, propertyId, roomId);

    public void Apply(Guid propertyId, Guid roomId, string? label, BedStatus status, long sourceVersion)
    {
        if (this.PropertyId != propertyId || this.RoomId != roomId)
        {
            throw new InvalidOperationException("A bed topology id cannot move to another room or property.");
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sourceVersion);

        if (sourceVersion > this.SourceVersion)
        {
            this.SourceVersion = sourceVersion;
            this.Status = status;
        }

        if (label is not null && sourceVersion > this.DetailsVersion)
        {
            this.Label = label;
            this.DetailsVersion = sourceVersion;
            this.IsKnown = true;
        }
    }
}

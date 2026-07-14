namespace BunkFy.Modules.Inventory.Domain.Events;

using Gma.Framework.Domain;

public sealed record ManualInventoryBlockCreatedDomainEvent : ScopedDomainEvent
{
    public ManualInventoryBlockCreatedDomainEvent(
        Guid eventId,
        DateTimeOffset occurredAtUtc,
        string scopeId,
        Guid blockId,
        Guid blockGroupId,
        Guid propertyId,
        Guid inventoryUnitId,
        DateOnly arrival,
        DateOnly departure,
        string reason,
        long blockVersion)
        : base(eventId, occurredAtUtc, scopeId)
    {
        this.BlockId = DomainEventGuards.RequireId(blockId, nameof(blockId));
        this.BlockGroupId = DomainEventGuards.RequireId(blockGroupId, nameof(blockGroupId));
        this.PropertyId = DomainEventGuards.RequireId(propertyId, nameof(propertyId));
        this.InventoryUnitId = DomainEventGuards.RequireId(inventoryUnitId, nameof(inventoryUnitId));
        this.Arrival = arrival;
        this.Departure = departure;
        this.Reason = reason;
        this.BlockVersion = blockVersion;
    }

    public Guid BlockId { get; }
    public Guid BlockGroupId { get; }
    public Guid PropertyId { get; }
    public Guid InventoryUnitId { get; }
    public DateOnly Arrival { get; }
    public DateOnly Departure { get; }
    public string Reason { get; }
    public long BlockVersion { get; }
}

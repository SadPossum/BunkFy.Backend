namespace BunkFy.Modules.Inventory.Domain.Events;

using Gma.Framework.Domain;

public sealed record ManualInventoryBlockReleasedDomainEvent : ScopedDomainEvent
{
    public ManualInventoryBlockReleasedDomainEvent(
        Guid eventId,
        DateTimeOffset occurredAtUtc,
        string scopeId,
        Guid blockId,
        Guid blockGroupId,
        Guid propertyId,
        Guid inventoryUnitId,
        long blockVersion,
        string? actorId = null)
        : base(eventId, occurredAtUtc, scopeId)
    {
        this.BlockId = DomainEventGuards.RequireId(blockId, nameof(blockId));
        this.BlockGroupId = DomainEventGuards.RequireId(blockGroupId, nameof(blockGroupId));
        this.PropertyId = DomainEventGuards.RequireId(propertyId, nameof(propertyId));
        this.InventoryUnitId = DomainEventGuards.RequireId(inventoryUnitId, nameof(inventoryUnitId));
        this.BlockVersion = blockVersion;
        this.ActorId = string.IsNullOrWhiteSpace(actorId) ? null : actorId.Trim();
    }

    public Guid BlockId { get; }
    public Guid BlockGroupId { get; }
    public Guid PropertyId { get; }
    public Guid InventoryUnitId { get; }
    public long BlockVersion { get; }
    public string? ActorId { get; }
}

namespace BunkFy.Modules.Inventory.Domain.Events;

using Gma.Framework.Domain;
using BunkFy.Modules.Inventory.Domain.Aggregates;

public sealed record RoomSalesModeChangedDomainEvent : ScopedDomainEvent
{
    public RoomSalesModeChangedDomainEvent(
        Guid eventId,
        DateTimeOffset occurredAtUtc,
        string scopeId,
        Guid propertyId,
        Guid roomId,
        RoomSalesMode salesMode,
        long configurationVersion,
        string? actorId = null)
        : base(eventId, occurredAtUtc, scopeId)
    {
        this.PropertyId = DomainEventGuards.RequireId(propertyId, nameof(propertyId));
        this.RoomId = DomainEventGuards.RequireId(roomId, nameof(roomId));
        this.SalesMode = salesMode;
        this.ConfigurationVersion = configurationVersion > 0
            ? configurationVersion
            : throw new ArgumentOutOfRangeException(nameof(configurationVersion));
        this.ActorId = string.IsNullOrWhiteSpace(actorId) ? null : actorId.Trim();
    }

    public Guid PropertyId { get; }
    public Guid RoomId { get; }
    public RoomSalesMode SalesMode { get; }
    public long ConfigurationVersion { get; }
    public string? ActorId { get; }
}

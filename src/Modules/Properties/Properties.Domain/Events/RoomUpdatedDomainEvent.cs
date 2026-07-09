namespace Properties.Domain.Events;

using Properties.Domain.Aggregates;
using Gma.Framework.Domain;

public sealed record RoomUpdatedDomainEvent : TenantDomainEvent
{
    public RoomUpdatedDomainEvent(
        Guid eventId,
        DateTimeOffset occurredAtUtc,
        Guid propertyId,
        Guid roomId,
        string tenantId,
        string name,
        string? buildingLabel,
        string? floorLabel,
        RoomState status)
        : base(eventId, occurredAtUtc, tenantId)
    {
        this.PropertyId = DomainEventGuards.RequireId(propertyId, nameof(propertyId));
        this.RoomId = DomainEventGuards.RequireId(roomId, nameof(roomId));
        this.Name = DomainEventGuards.NormalizeRequiredText(name, Room.RoomNameMaxLength, nameof(name));
        this.BuildingLabel = NormalizeOptionalLabel(buildingLabel, nameof(buildingLabel));
        this.FloorLabel = NormalizeOptionalLabel(floorLabel, nameof(floorLabel));
        this.Status = status;
    }

    public Guid PropertyId { get; }
    public Guid RoomId { get; }
    public string Name { get; }
    public string? BuildingLabel { get; }
    public string? FloorLabel { get; }
    public RoomState Status { get; }

    private static string? NormalizeOptionalLabel(string? value, string parameterName) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : DomainEventGuards.NormalizeRequiredText(value, Room.PhysicalLabelMaxLength, parameterName);
}

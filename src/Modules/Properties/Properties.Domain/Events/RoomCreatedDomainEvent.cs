namespace Properties.Domain.Events;

using Properties.Domain.Aggregates;
using Gma.Framework.Domain;

public sealed record RoomCreatedDomainEvent : ScopedDomainEvent
{
    public RoomCreatedDomainEvent(
        Guid eventId,
        DateTimeOffset occurredAtUtc,
        Guid propertyId,
        Guid roomId,
        string tenantId,
        string name,
        string? buildingLabel,
        string? floorLabel,
        RoomState status,
        long roomVersion)
        : base(eventId, occurredAtUtc, tenantId)
    {
        this.PropertyId = DomainEventGuards.RequireId(propertyId, nameof(propertyId));
        this.RoomId = DomainEventGuards.RequireId(roomId, nameof(roomId));
        this.Name = DomainEventGuards.NormalizeRequiredText(name, Room.RoomNameMaxLength, nameof(name));
        this.BuildingLabel = NormalizeOptionalLabel(buildingLabel, nameof(buildingLabel));
        this.FloorLabel = NormalizeOptionalLabel(floorLabel, nameof(floorLabel));
        this.Status = status;
        this.RoomVersion = roomVersion > 0
            ? roomVersion
            : throw new ArgumentOutOfRangeException(nameof(roomVersion));
    }

    public Guid PropertyId { get; }
    public Guid RoomId { get; }
    public string Name { get; }
    public string? BuildingLabel { get; }
    public string? FloorLabel { get; }
    public RoomState Status { get; }
    public long RoomVersion { get; }

    private static string? NormalizeOptionalLabel(string? value, string parameterName) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : DomainEventGuards.NormalizeRequiredText(value, Room.PhysicalLabelMaxLength, parameterName);
}

namespace Properties.Contracts;

using Gma.Framework.Messaging;
using Gma.Framework.Tenancy;
using Gma.Framework.Tenancy.Messaging;

[IntegrationEventName(EventType)]
[IntegrationEventVersion(EventVersion)]
[TenantScoped]
public sealed record BedAddedIntegrationEvent : TenantIntegrationEvent
{
    public const string EventType = "bed-added";
    public const int EventVersion = 1;

    public BedAddedIntegrationEvent(
        Guid eventId,
        string tenantId,
        DateTimeOffset occurredAtUtc,
        Guid propertyId,
        Guid roomId,
        Guid bedId,
        string label,
        BedStatus status)
        : base(eventId, tenantId, occurredAtUtc, EventType, EventVersion)
    {
        this.PropertyId = IntegrationEventContractGuards.RequireId(propertyId, nameof(propertyId));
        this.RoomId = IntegrationEventContractGuards.RequireId(roomId, nameof(roomId));
        this.BedId = IntegrationEventContractGuards.RequireId(bedId, nameof(bedId));
        this.Label = IntegrationEventContractGuards.NormalizeRequiredText(label, PropertiesContractLimits.BedLabelMaxLength, nameof(label));
        this.Status = PropertiesEventContractGuards.RequireKnown(status, nameof(status));
    }

    public Guid PropertyId { get; }
    public Guid RoomId { get; }
    public Guid BedId { get; }
    public string Label { get; }
    public BedStatus Status { get; }
}

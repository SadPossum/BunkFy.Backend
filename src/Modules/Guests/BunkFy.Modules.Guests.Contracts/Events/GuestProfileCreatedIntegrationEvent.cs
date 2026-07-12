namespace BunkFy.Modules.Guests.Contracts;

using Gma.Framework.Messaging;
using Gma.Framework.Tenancy;
using Gma.Framework.Tenancy.Messaging;

[IntegrationEventName(EventType)]
[IntegrationEventVersion(EventVersion)]
[TenantScoped]
public sealed record GuestProfileCreatedIntegrationEvent : TenantIntegrationEvent
{
    public const string EventType = "guest-profile-created";
    public const int EventVersion = 1;

    public GuestProfileCreatedIntegrationEvent(
        Guid eventId,
        string tenantId,
        DateTimeOffset occurredAtUtc,
        Guid guestId,
        Guid originPropertyId,
        GuestStatus status,
        long guestVersion)
        : base(eventId, tenantId, occurredAtUtc, EventType, EventVersion)
    {
        this.GuestId = IntegrationEventContractGuards.RequireId(guestId, nameof(guestId));
        this.OriginPropertyId = IntegrationEventContractGuards.RequireId(originPropertyId, nameof(originPropertyId));
        this.Status = status is GuestStatus.Active ? status : throw new ArgumentOutOfRangeException(nameof(status));
        this.GuestVersion = guestVersion > 0 ? guestVersion : throw new ArgumentOutOfRangeException(nameof(guestVersion));
    }

    public Guid GuestId { get; }
    public Guid OriginPropertyId { get; }
    public GuestStatus Status { get; }
    public long GuestVersion { get; }
}

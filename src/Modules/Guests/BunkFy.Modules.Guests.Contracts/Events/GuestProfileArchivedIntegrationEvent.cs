namespace BunkFy.Modules.Guests.Contracts;

using Gma.Framework.Messaging;
using Gma.Framework.Tenancy;
using Gma.Framework.Tenancy.Messaging;

[IntegrationEventName(EventType)]
[IntegrationEventVersion(EventVersion)]
[TenantScoped]
public sealed record GuestProfileArchivedIntegrationEvent : TenantIntegrationEvent
{
    public const string EventType = "guest-profile-archived";
    public const int EventVersion = 1;

    public GuestProfileArchivedIntegrationEvent(
        Guid eventId,
        string tenantId,
        DateTimeOffset occurredAtUtc,
        Guid guestId,
        long guestVersion)
        : base(eventId, tenantId, occurredAtUtc, EventType, EventVersion)
    {
        this.GuestId = IntegrationEventContractGuards.RequireId(guestId, nameof(guestId));
        this.GuestVersion = guestVersion > 0 ? guestVersion : throw new ArgumentOutOfRangeException(nameof(guestVersion));
    }

    public Guid GuestId { get; }
    public long GuestVersion { get; }
}

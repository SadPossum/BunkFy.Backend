namespace BunkFy.Modules.Reservations.Contracts;

using Gma.Framework.Messaging;
using Gma.Framework.Tenancy;
using Gma.Framework.Tenancy.Messaging;

[IntegrationEventName(EventType)]
[IntegrationEventVersion(EventVersion)]
[TenantScoped]
public sealed record ReservationCancelledIntegrationEvent : TenantIntegrationEvent
{
    public const string EventType = "reservation-cancelled";
    public const int EventVersion = 1;

    public ReservationCancelledIntegrationEvent(
        Guid eventId,
        string tenantId,
        DateTimeOffset occurredAtUtc,
        Guid reservationId,
        Guid propertyId,
        long reservationVersion)
        : base(eventId, tenantId, occurredAtUtc, EventType, EventVersion)
    {
        this.ReservationId = IntegrationEventContractGuards.RequireId(reservationId, nameof(reservationId));
        this.PropertyId = IntegrationEventContractGuards.RequireId(propertyId, nameof(propertyId));
        this.ReservationVersion = reservationVersion > 0 ? reservationVersion : throw new ArgumentOutOfRangeException(nameof(reservationVersion));
    }

    public Guid ReservationId { get; }
    public Guid PropertyId { get; }
    public long ReservationVersion { get; }
}

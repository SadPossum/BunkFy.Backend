namespace BunkFy.Modules.Reservations.Contracts;

using Gma.Framework.Messaging;
using Gma.Framework.Tenancy;
using Gma.Framework.Tenancy.Messaging;

[IntegrationEventName(EventType)]
[IntegrationEventVersion(EventVersion)]
[TenantScoped]
public sealed record ReservationCreatedIntegrationEvent : TenantIntegrationEvent
{
    public const string EventType = "reservation-created";
    public const int EventVersion = 1;

    public ReservationCreatedIntegrationEvent(
        Guid eventId,
        string tenantId,
        DateTimeOffset occurredAtUtc,
        Guid reservationId,
        Guid propertyId,
        DateOnly arrival,
        DateOnly departure,
        long reservationVersion)
        : base(eventId, tenantId, occurredAtUtc, EventType, EventVersion)
    {
        this.ReservationId = IntegrationEventContractGuards.RequireId(reservationId, nameof(reservationId));
        this.PropertyId = IntegrationEventContractGuards.RequireId(propertyId, nameof(propertyId));
        this.Arrival = arrival;
        this.Departure = departure > arrival ? departure : throw new ArgumentOutOfRangeException(nameof(departure));
        this.ReservationVersion = reservationVersion > 0 ? reservationVersion : throw new ArgumentOutOfRangeException(nameof(reservationVersion));
    }

    public Guid ReservationId { get; }
    public Guid PropertyId { get; }
    public DateOnly Arrival { get; }
    public DateOnly Departure { get; }
    public long ReservationVersion { get; }
}

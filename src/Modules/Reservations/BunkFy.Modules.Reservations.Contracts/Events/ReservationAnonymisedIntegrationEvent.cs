namespace BunkFy.Modules.Reservations.Contracts;

using Gma.Framework.Messaging;
using Gma.Framework.Tenancy;
using Gma.Framework.Tenancy.Messaging;

[IntegrationEventName(EventType)]
[IntegrationEventVersion(EventVersion)]
[TenantScoped]
public sealed record ReservationAnonymisedIntegrationEvent : TenantIntegrationEvent
{
    public const string EventType = "reservation-anonymised";
    public const int EventVersion = 1;

    public ReservationAnonymisedIntegrationEvent(
        Guid eventId,
        string tenantId,
        DateTimeOffset occurredAtUtc,
        Guid propertyId,
        Guid reservationId,
        long reservationVersion,
        long detailsRevision)
        : base(eventId, tenantId, occurredAtUtc, EventType, EventVersion)
    {
        this.PropertyId = IntegrationEventContractGuards.RequireId(
            propertyId,
            nameof(propertyId));
        this.ReservationId = IntegrationEventContractGuards.RequireId(
            reservationId,
            nameof(reservationId));
        this.ReservationVersion = reservationVersion > 1
            ? reservationVersion
            : throw new ArgumentOutOfRangeException(nameof(reservationVersion));
        this.DetailsRevision = detailsRevision > 1
            ? detailsRevision
            : throw new ArgumentOutOfRangeException(nameof(detailsRevision));
    }

    public Guid PropertyId { get; }
    public Guid ReservationId { get; }
    public long ReservationVersion { get; }
    public long DetailsRevision { get; }
}

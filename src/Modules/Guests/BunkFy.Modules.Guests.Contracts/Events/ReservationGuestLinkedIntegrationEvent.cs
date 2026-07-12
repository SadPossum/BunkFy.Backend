namespace BunkFy.Modules.Guests.Contracts;

using Gma.Framework.Messaging;
using Gma.Framework.Tenancy;
using Gma.Framework.Tenancy.Messaging;

[IntegrationEventName(EventType)]
[IntegrationEventVersion(EventVersion)]
[TenantScoped]
public sealed record ReservationGuestLinkedIntegrationEvent : TenantIntegrationEvent
{
    public const string EventType = "reservation-guest-linked";
    public const int EventVersion = 1;

    public ReservationGuestLinkedIntegrationEvent(
        Guid eventId,
        string tenantId,
        DateTimeOffset occurredAtUtc,
        Guid propertyId,
        Guid reservationId,
        Guid guestId,
        GuestStayRole role,
        DateOnly arrival,
        DateOnly departure,
        GuestStayStatus status,
        DateOnly? checkedInBusinessDate,
        DateOnly? noShowBusinessDate,
        DateOnly? checkedOutBusinessDate,
        long reservationVersion)
        : base(eventId, tenantId, occurredAtUtc, EventType, EventVersion)
    {
        this.PropertyId = IntegrationEventContractGuards.RequireId(propertyId, nameof(propertyId));
        this.ReservationId = IntegrationEventContractGuards.RequireId(reservationId, nameof(reservationId));
        this.GuestId = IntegrationEventContractGuards.RequireId(guestId, nameof(guestId));
        this.Role = role is GuestStayRole.Primary ? role : throw new ArgumentOutOfRangeException(nameof(role));
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(arrival, departure);

        this.Arrival = arrival;
        this.Departure = departure;
        this.Status = status != GuestStayStatus.Unknown && Enum.IsDefined(status)
            ? status
            : throw new ArgumentOutOfRangeException(nameof(status));
        this.CheckedInBusinessDate = checkedInBusinessDate;
        this.NoShowBusinessDate = noShowBusinessDate;
        this.CheckedOutBusinessDate = checkedOutBusinessDate;
        this.ReservationVersion = reservationVersion > 0
            ? reservationVersion
            : throw new ArgumentOutOfRangeException(nameof(reservationVersion));
    }

    public Guid PropertyId { get; }
    public Guid ReservationId { get; }
    public Guid GuestId { get; }
    public GuestStayRole Role { get; }
    public DateOnly Arrival { get; }
    public DateOnly Departure { get; }
    public GuestStayStatus Status { get; }
    public DateOnly? CheckedInBusinessDate { get; }
    public DateOnly? NoShowBusinessDate { get; }
    public DateOnly? CheckedOutBusinessDate { get; }
    public long ReservationVersion { get; }
}

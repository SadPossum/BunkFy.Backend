namespace BunkFy.Modules.Reservations.Contracts;

using Gma.Framework.Messaging;
using Gma.Framework.Tenancy;
using Gma.Framework.Tenancy.Messaging;

[IntegrationEventName(EventType)]
[IntegrationEventVersion(EventVersion)]
[TenantScoped]
public sealed record ReservationArrivalReminderDueIntegrationEvent : TenantIntegrationEvent
{
    public const string EventType = "reservation-arrival-reminder-due";
    public const int EventVersion = 1;

    public ReservationArrivalReminderDueIntegrationEvent(
        Guid eventId,
        string tenantId,
        DateTimeOffset occurredAtUtc,
        Guid reservationId,
        Guid propertyId,
        string primaryGuestName,
        DateOnly arrival,
        TimeOnly expectedArrivalTime,
        string timeZoneId,
        long detailsRevision)
        : base(eventId, tenantId, occurredAtUtc, EventType, EventVersion)
    {
        this.ReservationId = IntegrationEventContractGuards.RequireId(reservationId, nameof(reservationId));
        this.PropertyId = IntegrationEventContractGuards.RequireId(propertyId, nameof(propertyId));
        this.PrimaryGuestName = IntegrationEventContractGuards.NormalizeRequiredText(
            primaryGuestName,
            ReservationsContractLimits.PrimaryGuestNameMaxLength,
            nameof(primaryGuestName));
        this.Arrival = arrival;
        this.ExpectedArrivalTime = expectedArrivalTime;
        this.TimeZoneId = IntegrationEventContractGuards.NormalizeRequiredText(
            timeZoneId,
            ReservationsContractLimits.TimeZoneIdMaxLength,
            nameof(timeZoneId));
        this.DetailsRevision = detailsRevision > 0
            ? detailsRevision
            : throw new ArgumentOutOfRangeException(nameof(detailsRevision));
    }

    public Guid ReservationId { get; }
    public Guid PropertyId { get; }
    public string PrimaryGuestName { get; }
    public DateOnly Arrival { get; }
    public TimeOnly ExpectedArrivalTime { get; }
    public string TimeZoneId { get; }
    public long DetailsRevision { get; }
}

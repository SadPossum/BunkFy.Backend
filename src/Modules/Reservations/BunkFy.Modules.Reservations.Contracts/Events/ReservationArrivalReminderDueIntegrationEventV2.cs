namespace BunkFy.Modules.Reservations.Contracts;

using Gma.Framework.Messaging;
using Gma.Framework.Tenancy;
using Gma.Framework.Tenancy.Messaging;

[IntegrationEventName(EventType)]
[IntegrationEventVersion(EventVersion)]
[TenantScoped]
public sealed record ReservationArrivalReminderDueIntegrationEventV2 : TenantIntegrationEvent
{
    public const string EventType = ReservationArrivalReminderDueIntegrationEvent.EventType;
    public const int EventVersion = 2;

    public ReservationArrivalReminderDueIntegrationEventV2(
        Guid eventId,
        string tenantId,
        DateTimeOffset occurredAtUtc,
        Guid reservationId,
        Guid propertyId,
        DateOnly arrival,
        TimeOnly expectedArrivalTime,
        string timeZoneId,
        long detailsRevision)
        : base(eventId, tenantId, occurredAtUtc, EventType, EventVersion)
    {
        this.ReservationId = IntegrationEventContractGuards.RequireId(reservationId, nameof(reservationId));
        this.PropertyId = IntegrationEventContractGuards.RequireId(propertyId, nameof(propertyId));
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
    public DateOnly Arrival { get; }
    public TimeOnly ExpectedArrivalTime { get; }
    public string TimeZoneId { get; }
    public long DetailsRevision { get; }
}

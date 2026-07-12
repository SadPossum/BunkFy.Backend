namespace BunkFy.Modules.Reservations.Contracts;

using Gma.Framework.Messaging;
using Gma.Framework.Tenancy;
using Gma.Framework.Tenancy.Messaging;

[IntegrationEventName(EventType)]
[IntegrationEventVersion(EventVersion)]
[TenantScoped]
public sealed record ReservationCheckedInIntegrationEvent : TenantIntegrationEvent
{
    public const string EventType = "reservation-checked-in";
    public const int EventVersion = 1;

    public ReservationCheckedInIntegrationEvent(
        Guid eventId,
        string tenantId,
        DateTimeOffset occurredAtUtc,
        Guid reservationId,
        Guid propertyId,
        DateOnly businessDate,
        string actorId,
        long reservationVersion)
        : base(eventId, tenantId, occurredAtUtc, EventType, EventVersion)
    {
        this.ReservationId = IntegrationEventContractGuards.RequireId(reservationId, nameof(reservationId));
        this.PropertyId = IntegrationEventContractGuards.RequireId(propertyId, nameof(propertyId));
        this.BusinessDate = businessDate;
        this.ActorId = RequireActor(actorId);
        this.ReservationVersion = reservationVersion > 0
            ? reservationVersion
            : throw new ArgumentOutOfRangeException(nameof(reservationVersion));
    }

    public Guid ReservationId { get; }
    public Guid PropertyId { get; }
    public DateOnly BusinessDate { get; }
    public string ActorId { get; }
    public long ReservationVersion { get; }

    private static string RequireActor(string value)
    {
        string normalized = value?.Trim() ?? string.Empty;
        return normalized.Length is > 0 and <= ReservationsContractLimits.ActorIdMaxLength
            ? normalized
            : throw new ArgumentException("Actor id is invalid.", nameof(value));
    }
}

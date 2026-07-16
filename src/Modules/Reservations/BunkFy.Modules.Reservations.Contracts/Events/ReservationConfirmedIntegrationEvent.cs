namespace BunkFy.Modules.Reservations.Contracts;

using Gma.Framework.Messaging;
using Gma.Framework.Tenancy;
using Gma.Framework.Tenancy.Messaging;

[IntegrationEventName(EventType)]
[IntegrationEventVersion(EventVersion)]
[TenantScoped]
public sealed record ReservationConfirmedIntegrationEvent : TenantIntegrationEvent
{
    public const string EventType = "reservation-confirmed";
    public const int EventVersion = 1;

    public ReservationConfirmedIntegrationEvent(
        Guid eventId,
        string tenantId,
        DateTimeOffset occurredAtUtc,
        Guid reservationId,
        Guid propertyId,
        Guid allocationId,
        long reservationVersion,
        string? actorId = null)
        : base(eventId, tenantId, occurredAtUtc, EventType, EventVersion)
    {
        this.ReservationId = IntegrationEventContractGuards.RequireId(reservationId, nameof(reservationId));
        this.PropertyId = IntegrationEventContractGuards.RequireId(propertyId, nameof(propertyId));
        this.AllocationId = IntegrationEventContractGuards.RequireId(allocationId, nameof(allocationId));
        this.ReservationVersion = reservationVersion > 0 ? reservationVersion : throw new ArgumentOutOfRangeException(nameof(reservationVersion));
        this.ActorId = OptionalActor(actorId);
    }

    public Guid ReservationId { get; }
    public Guid PropertyId { get; }
    public Guid AllocationId { get; }
    public long ReservationVersion { get; }
    public string? ActorId { get; }

    private static string? OptionalActor(string? value)
    {
        string? normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        return normalized is null || normalized.Length <= ReservationsContractLimits.ActorIdMaxLength
            ? normalized
            : throw new ArgumentException("Actor id is invalid.", nameof(value));
    }
}

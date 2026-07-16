namespace BunkFy.Modules.Reservations.Contracts;

using Gma.Framework.Messaging;
using Gma.Framework.Tenancy;
using Gma.Framework.Tenancy.Messaging;
using BunkFy.Modules.Inventory.Contracts;

[IntegrationEventName(EventType)]
[IntegrationEventVersion(EventVersion)]
[TenantScoped]
public sealed record ReservationAllocationRejectedIntegrationEvent : TenantIntegrationEvent
{
    public const string EventType = "reservation-allocation-rejected";
    public const int EventVersion = 1;

    public ReservationAllocationRejectedIntegrationEvent(
        Guid eventId,
        string tenantId,
        DateTimeOffset occurredAtUtc,
        Guid reservationId,
        Guid propertyId,
        InventoryAllocationRejectionReason reason,
        long reservationVersion,
        string? actorId = null)
        : base(eventId, tenantId, occurredAtUtc, EventType, EventVersion)
    {
        this.ReservationId = IntegrationEventContractGuards.RequireId(reservationId, nameof(reservationId));
        this.PropertyId = IntegrationEventContractGuards.RequireId(propertyId, nameof(propertyId));
        this.Reason = reason != InventoryAllocationRejectionReason.Unknown && Enum.IsDefined(reason)
            ? reason
            : throw new ArgumentOutOfRangeException(nameof(reason));
        this.ReservationVersion = reservationVersion > 0 ? reservationVersion : throw new ArgumentOutOfRangeException(nameof(reservationVersion));
        this.ActorId = OptionalActor(actorId);
    }

    public Guid ReservationId { get; }
    public Guid PropertyId { get; }
    public InventoryAllocationRejectionReason Reason { get; }
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

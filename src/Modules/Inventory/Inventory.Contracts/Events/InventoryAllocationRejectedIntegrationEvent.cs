namespace Inventory.Contracts;

using Gma.Framework.Messaging;
using Gma.Framework.Tenancy;
using Gma.Framework.Tenancy.Messaging;

[IntegrationEventName(EventType)]
[IntegrationEventVersion(EventVersion)]
[TenantScoped]
public sealed record InventoryAllocationRejectedIntegrationEvent : TenantIntegrationEvent
{
    public const string EventType = "inventory-allocation-rejected";
    public const int EventVersion = 1;

    public InventoryAllocationRejectedIntegrationEvent(
        Guid eventId,
        string tenantId,
        DateTimeOffset occurredAtUtc,
        Guid reservationId,
        Guid allocationRequestId,
        Guid propertyId,
        InventoryAllocationRejectionReason reason)
        : base(eventId, tenantId, occurredAtUtc, EventType, EventVersion)
    {
        this.ReservationId = IntegrationEventContractGuards.RequireId(reservationId, nameof(reservationId));
        this.AllocationRequestId = IntegrationEventContractGuards.RequireId(allocationRequestId, nameof(allocationRequestId));
        this.PropertyId = IntegrationEventContractGuards.RequireId(propertyId, nameof(propertyId));
        this.Reason = reason != InventoryAllocationRejectionReason.Unknown && Enum.IsDefined(reason)
            ? reason
            : throw new ArgumentOutOfRangeException(nameof(reason));
    }

    public Guid ReservationId { get; }
    public Guid AllocationRequestId { get; }
    public Guid PropertyId { get; }
    public InventoryAllocationRejectionReason Reason { get; }
}

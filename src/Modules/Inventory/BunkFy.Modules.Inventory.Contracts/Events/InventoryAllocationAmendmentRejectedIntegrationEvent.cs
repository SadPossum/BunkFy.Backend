namespace BunkFy.Modules.Inventory.Contracts;

using Gma.Framework.Messaging;
using Gma.Framework.Tenancy;
using Gma.Framework.Tenancy.Messaging;

[IntegrationEventName(EventType)]
[IntegrationEventVersion(EventVersion)]
[TenantScoped]
public sealed record InventoryAllocationAmendmentRejectedIntegrationEvent : TenantIntegrationEvent
{
    public const string EventType = "inventory-allocation-amendment-rejected";
    public const int EventVersion = 1;

    public InventoryAllocationAmendmentRejectedIntegrationEvent(
        Guid eventId,
        string tenantId,
        DateTimeOffset occurredAtUtc,
        Guid amendmentRequestId,
        Guid allocationId,
        Guid reservationId,
        Guid propertyId,
        InventoryAllocationRejectionReason reason)
        : base(eventId, tenantId, occurredAtUtc, EventType, EventVersion)
    {
        this.AmendmentRequestId = IntegrationEventContractGuards.RequireId(amendmentRequestId, nameof(amendmentRequestId));
        this.AllocationId = IntegrationEventContractGuards.RequireId(allocationId, nameof(allocationId));
        this.ReservationId = IntegrationEventContractGuards.RequireId(reservationId, nameof(reservationId));
        this.PropertyId = IntegrationEventContractGuards.RequireId(propertyId, nameof(propertyId));
        this.Reason = reason != InventoryAllocationRejectionReason.Unknown && Enum.IsDefined(reason)
            ? reason
            : throw new ArgumentOutOfRangeException(nameof(reason));
    }

    public Guid AmendmentRequestId { get; }
    public Guid AllocationId { get; }
    public Guid ReservationId { get; }
    public Guid PropertyId { get; }
    public InventoryAllocationRejectionReason Reason { get; }
}

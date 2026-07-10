namespace Inventory.Contracts;

using Gma.Framework.Messaging;
using Gma.Framework.Tenancy;
using Gma.Framework.Tenancy.Messaging;

[IntegrationEventName(EventType)]
[IntegrationEventVersion(EventVersion)]
[TenantScoped]
public sealed record InventoryAllocationReleaseRejectedIntegrationEvent : TenantIntegrationEvent
{
    public const string EventType = "inventory-allocation-release-rejected";
    public const int EventVersion = 1;

    public InventoryAllocationReleaseRejectedIntegrationEvent(
        Guid eventId,
        string tenantId,
        DateTimeOffset occurredAtUtc,
        Guid allocationId,
        Guid reservationId,
        Guid releaseRequestId,
        InventoryAllocationReleaseRejectionReason reason)
        : base(eventId, tenantId, occurredAtUtc, EventType, EventVersion)
    {
        this.AllocationId = IntegrationEventContractGuards.RequireId(allocationId, nameof(allocationId));
        this.ReservationId = IntegrationEventContractGuards.RequireId(reservationId, nameof(reservationId));
        this.ReleaseRequestId = IntegrationEventContractGuards.RequireId(releaseRequestId, nameof(releaseRequestId));
        this.Reason = reason != InventoryAllocationReleaseRejectionReason.Unknown && Enum.IsDefined(reason)
            ? reason
            : throw new ArgumentOutOfRangeException(nameof(reason));
    }

    public Guid AllocationId { get; }
    public Guid ReservationId { get; }
    public Guid ReleaseRequestId { get; }
    public InventoryAllocationReleaseRejectionReason Reason { get; }
}

namespace BunkFy.Modules.Inventory.Contracts;

using Gma.Framework.Messaging;
using Gma.Framework.Tenancy;
using Gma.Framework.Tenancy.Messaging;

[IntegrationEventName(EventType)]
[IntegrationEventVersion(EventVersion)]
[TenantScoped]
public sealed record InventoryAllocationReleaseRequestedIntegrationEvent : TenantIntegrationEvent
{
    public const string EventType = "inventory-allocation-release-requested";
    public const int EventVersion = 1;

    public InventoryAllocationReleaseRequestedIntegrationEvent(
        Guid eventId,
        string tenantId,
        DateTimeOffset occurredAtUtc,
        Guid reservationId,
        Guid allocationId,
        Guid releaseRequestId,
        long expectedAllocationVersion)
        : base(eventId, tenantId, occurredAtUtc, EventType, EventVersion)
    {
        this.ReservationId = IntegrationEventContractGuards.RequireId(reservationId, nameof(reservationId));
        this.AllocationId = IntegrationEventContractGuards.RequireId(allocationId, nameof(allocationId));
        this.ReleaseRequestId = IntegrationEventContractGuards.RequireId(releaseRequestId, nameof(releaseRequestId));
        this.ExpectedAllocationVersion = expectedAllocationVersion > 0
            ? expectedAllocationVersion
            : throw new ArgumentOutOfRangeException(nameof(expectedAllocationVersion));
    }

    public Guid ReservationId { get; }
    public Guid AllocationId { get; }
    public Guid ReleaseRequestId { get; }
    public long ExpectedAllocationVersion { get; }
}

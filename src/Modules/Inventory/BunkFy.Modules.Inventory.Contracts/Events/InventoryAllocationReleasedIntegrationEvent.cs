namespace BunkFy.Modules.Inventory.Contracts;

using Gma.Framework.Messaging;
using Gma.Framework.Tenancy;
using Gma.Framework.Tenancy.Messaging;

[IntegrationEventName(EventType)]
[IntegrationEventVersion(EventVersion)]
[TenantScoped]
public sealed record InventoryAllocationReleasedIntegrationEvent : TenantIntegrationEvent
{
    public const string EventType = "inventory-allocation-released";
    public const int EventVersion = 1;

    public InventoryAllocationReleasedIntegrationEvent(
        Guid eventId,
        string tenantId,
        DateTimeOffset occurredAtUtc,
        Guid allocationId,
        Guid reservationId,
        Guid releaseRequestId,
        long allocationVersion)
        : base(eventId, tenantId, occurredAtUtc, EventType, EventVersion)
    {
        this.AllocationId = IntegrationEventContractGuards.RequireId(allocationId, nameof(allocationId));
        this.ReservationId = IntegrationEventContractGuards.RequireId(reservationId, nameof(reservationId));
        this.ReleaseRequestId = IntegrationEventContractGuards.RequireId(releaseRequestId, nameof(releaseRequestId));
        this.AllocationVersion = allocationVersion > 0
            ? allocationVersion
            : throw new ArgumentOutOfRangeException(nameof(allocationVersion));
    }

    public Guid AllocationId { get; }
    public Guid ReservationId { get; }
    public Guid ReleaseRequestId { get; }
    public long AllocationVersion { get; }
}

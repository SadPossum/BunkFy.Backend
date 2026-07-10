namespace Inventory.Contracts;

using Gma.Framework.Messaging;
using Gma.Framework.Tenancy;
using Gma.Framework.Tenancy.Messaging;

[IntegrationEventName(EventType)]
[IntegrationEventVersion(EventVersion)]
[TenantScoped]
public sealed record InventoryAllocationConfirmedIntegrationEvent : TenantIntegrationEvent
{
    public const string EventType = "inventory-allocation-confirmed";
    public const int EventVersion = 1;

    public InventoryAllocationConfirmedIntegrationEvent(
        Guid eventId,
        string tenantId,
        DateTimeOffset occurredAtUtc,
        Guid allocationId,
        Guid reservationId,
        Guid allocationRequestId,
        Guid propertyId,
        DateOnly arrival,
        DateOnly departure,
        IReadOnlyCollection<Guid> inventoryUnitIds,
        long allocationVersion)
        : base(eventId, tenantId, occurredAtUtc, EventType, EventVersion)
    {
        this.AllocationId = IntegrationEventContractGuards.RequireId(allocationId, nameof(allocationId));
        this.ReservationId = IntegrationEventContractGuards.RequireId(reservationId, nameof(reservationId));
        this.AllocationRequestId = IntegrationEventContractGuards.RequireId(allocationRequestId, nameof(allocationRequestId));
        this.PropertyId = IntegrationEventContractGuards.RequireId(propertyId, nameof(propertyId));
        this.Arrival = arrival;
        this.Departure = departure > arrival ? departure : throw new ArgumentOutOfRangeException(nameof(departure));
        this.InventoryUnitIds = RequireUnits(inventoryUnitIds);
        this.AllocationVersion = allocationVersion > 0
            ? allocationVersion
            : throw new ArgumentOutOfRangeException(nameof(allocationVersion));
    }

    public Guid AllocationId { get; }
    public Guid ReservationId { get; }
    public Guid AllocationRequestId { get; }
    public Guid PropertyId { get; }
    public DateOnly Arrival { get; }
    public DateOnly Departure { get; }
    public IReadOnlyCollection<Guid> InventoryUnitIds { get; }
    public long AllocationVersion { get; }

    private static Guid[] RequireUnits(IReadOnlyCollection<Guid> inventoryUnitIds)
    {
        ArgumentNullException.ThrowIfNull(inventoryUnitIds);
        Guid[] units = inventoryUnitIds.ToArray();
        return units.Length is > 0 and <= InventoryContractLimits.MaximumUnitsPerAllocation &&
               units.All(id => id != Guid.Empty) &&
               units.Distinct().Count() == units.Length
            ? units
            : throw new ArgumentException("Confirmed allocation units are invalid.", nameof(inventoryUnitIds));
    }
}

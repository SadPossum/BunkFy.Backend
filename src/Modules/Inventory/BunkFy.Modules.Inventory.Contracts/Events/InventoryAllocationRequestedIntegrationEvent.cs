namespace BunkFy.Modules.Inventory.Contracts;

using Gma.Framework.Messaging;
using Gma.Framework.Tenancy;
using Gma.Framework.Tenancy.Messaging;

[IntegrationEventName(EventType)]
[IntegrationEventVersion(EventVersion)]
[TenantScoped]
public sealed record InventoryAllocationRequestedIntegrationEvent : TenantIntegrationEvent
{
    public const string EventType = "inventory-allocation-requested";
    public const int EventVersion = 1;

    public InventoryAllocationRequestedIntegrationEvent(
        Guid eventId,
        string tenantId,
        DateTimeOffset occurredAtUtc,
        Guid reservationId,
        Guid allocationRequestId,
        Guid propertyId,
        DateOnly arrival,
        DateOnly departure,
        IReadOnlyCollection<Guid> inventoryUnitIds)
        : base(eventId, tenantId, occurredAtUtc, EventType, EventVersion)
    {
        this.ReservationId = IntegrationEventContractGuards.RequireId(reservationId, nameof(reservationId));
        this.AllocationRequestId = IntegrationEventContractGuards.RequireId(allocationRequestId, nameof(allocationRequestId));
        this.PropertyId = IntegrationEventContractGuards.RequireId(propertyId, nameof(propertyId));
        this.Arrival = arrival;
        this.Departure = departure > arrival
            ? departure
            : throw new ArgumentOutOfRangeException(nameof(departure));
        ArgumentNullException.ThrowIfNull(inventoryUnitIds);
        Guid[] units = inventoryUnitIds.ToArray();
        if (units.Length is 0 or > InventoryContractLimits.MaximumUnitsPerAllocation ||
            units.Any(id => id == Guid.Empty) ||
            units.Distinct().Count() != units.Length)
        {
            throw new ArgumentException("Allocation units must contain unique, non-empty ids within the supported limit.", nameof(inventoryUnitIds));
        }

        this.InventoryUnitIds = units;
    }

    public Guid ReservationId { get; }
    public Guid AllocationRequestId { get; }
    public Guid PropertyId { get; }
    public DateOnly Arrival { get; }
    public DateOnly Departure { get; }
    public IReadOnlyCollection<Guid> InventoryUnitIds { get; }
}

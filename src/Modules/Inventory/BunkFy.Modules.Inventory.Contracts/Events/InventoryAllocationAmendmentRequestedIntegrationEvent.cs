namespace BunkFy.Modules.Inventory.Contracts;

using Gma.Framework.Messaging;
using Gma.Framework.Tenancy;
using Gma.Framework.Tenancy.Messaging;

[IntegrationEventName(EventType)]
[IntegrationEventVersion(EventVersion)]
[TenantScoped]
public sealed record InventoryAllocationAmendmentRequestedIntegrationEvent : TenantIntegrationEvent
{
    public const string EventType = "inventory-allocation-amendment-requested";
    public const int EventVersion = 1;

    public InventoryAllocationAmendmentRequestedIntegrationEvent(
        Guid eventId,
        string tenantId,
        DateTimeOffset occurredAtUtc,
        Guid amendmentRequestId,
        Guid allocationId,
        Guid reservationId,
        Guid propertyId,
        long expectedAllocationVersion,
        DateOnly arrival,
        DateOnly departure,
        IReadOnlyCollection<Guid> inventoryUnitIds)
        : base(eventId, tenantId, occurredAtUtc, EventType, EventVersion)
    {
        this.AmendmentRequestId = IntegrationEventContractGuards.RequireId(amendmentRequestId, nameof(amendmentRequestId));
        this.AllocationId = IntegrationEventContractGuards.RequireId(allocationId, nameof(allocationId));
        this.ReservationId = IntegrationEventContractGuards.RequireId(reservationId, nameof(reservationId));
        this.PropertyId = IntegrationEventContractGuards.RequireId(propertyId, nameof(propertyId));
        this.ExpectedAllocationVersion = expectedAllocationVersion > 0
            ? expectedAllocationVersion
            : throw new ArgumentOutOfRangeException(nameof(expectedAllocationVersion));
        this.Arrival = arrival;
        this.Departure = departure > arrival ? departure : throw new ArgumentOutOfRangeException(nameof(departure));
        this.InventoryUnitIds = RequireUnits(inventoryUnitIds);
    }

    public Guid AmendmentRequestId { get; }
    public Guid AllocationId { get; }
    public Guid ReservationId { get; }
    public Guid PropertyId { get; }
    public long ExpectedAllocationVersion { get; }
    public DateOnly Arrival { get; }
    public DateOnly Departure { get; }
    public IReadOnlyCollection<Guid> InventoryUnitIds { get; }

    private static Guid[] RequireUnits(IReadOnlyCollection<Guid> inventoryUnitIds)
    {
        ArgumentNullException.ThrowIfNull(inventoryUnitIds);
        Guid[] units = inventoryUnitIds.ToArray();
        return units.Length is > 0 and <= InventoryContractLimits.MaximumUnitsPerAllocation &&
               units.All(id => id != Guid.Empty) && units.Distinct().Count() == units.Length
            ? units
            : throw new ArgumentException("Allocation amendment units are invalid.", nameof(inventoryUnitIds));
    }
}

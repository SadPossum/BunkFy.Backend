namespace BunkFy.Modules.Inventory.Contracts;

using Gma.Framework.Messaging;
using Gma.Framework.Tenancy;
using Gma.Framework.Tenancy.Messaging;

[IntegrationEventName(EventType)]
[IntegrationEventVersion(EventVersion)]
[TenantScoped]
public sealed record InventoryAllocationAmendmentConfirmedIntegrationEvent : TenantIntegrationEvent
{
    public const string EventType = "inventory-allocation-amendment-confirmed";
    public const int EventVersion = 1;

    public InventoryAllocationAmendmentConfirmedIntegrationEvent(
        Guid eventId,
        string tenantId,
        DateTimeOffset occurredAtUtc,
        Guid amendmentRequestId,
        Guid allocationId,
        Guid reservationId,
        Guid propertyId,
        DateOnly arrival,
        DateOnly departure,
        IReadOnlyCollection<Guid> inventoryUnitIds,
        long allocationVersion)
        : base(eventId, tenantId, occurredAtUtc, EventType, EventVersion)
    {
        this.AmendmentRequestId = IntegrationEventContractGuards.RequireId(amendmentRequestId, nameof(amendmentRequestId));
        this.AllocationId = IntegrationEventContractGuards.RequireId(allocationId, nameof(allocationId));
        this.ReservationId = IntegrationEventContractGuards.RequireId(reservationId, nameof(reservationId));
        this.PropertyId = IntegrationEventContractGuards.RequireId(propertyId, nameof(propertyId));
        this.Arrival = arrival;
        this.Departure = departure > arrival ? departure : throw new ArgumentOutOfRangeException(nameof(departure));
        this.InventoryUnitIds = RequireUnits(inventoryUnitIds);
        this.AllocationVersion = allocationVersion > 0
            ? allocationVersion
            : throw new ArgumentOutOfRangeException(nameof(allocationVersion));
    }

    public Guid AmendmentRequestId { get; }
    public Guid AllocationId { get; }
    public Guid ReservationId { get; }
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
               units.All(id => id != Guid.Empty) && units.Distinct().Count() == units.Length
            ? units
            : throw new ArgumentException("Confirmed allocation amendment units are invalid.", nameof(inventoryUnitIds));
    }
}

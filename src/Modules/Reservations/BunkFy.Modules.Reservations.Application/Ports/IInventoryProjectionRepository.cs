namespace BunkFy.Modules.Reservations.Application.Ports;

using BunkFy.Modules.Inventory.Contracts;

public interface IInventoryProjectionRepository
{
    Task<InventoryUnitSelectionValidation> ValidateSelectionAsync(
        Guid propertyId,
        IReadOnlyCollection<Guid> inventoryUnitIds,
        CancellationToken cancellationToken);

    Task ApplyUnitAsync(ReservationInventoryUnitWriteModel unit, CancellationToken cancellationToken);
    Task ApplyBlockAsync(ReservationInventoryBlockWriteModel block, CancellationToken cancellationToken);
    Task ReleaseBlockAsync(
        string scopeId,
        Guid propertyId,
        Guid inventoryUnitId,
        Guid blockId,
        long version,
        CancellationToken cancellationToken);
    Task ApplyAllocationAsync(ReservationInventoryAllocationWriteModel allocation, CancellationToken cancellationToken);
    Task ReleaseAllocationAsync(
        string scopeId,
        Guid allocationId,
        Guid reservationId,
        long version,
        CancellationToken cancellationToken);
}

public enum InventoryUnitSelectionValidation
{
    Unknown = 0,
    Valid = 1,
    UnitNotFound = 2,
    PropertyMismatch = 3
}

public sealed record ReservationInventoryUnitWriteModel(
    string ScopeId,
    Guid InventoryUnitId,
    Guid PropertyId,
    Guid RoomId,
    Guid? BedId,
    InventoryUnitKind Kind,
    string Label,
    bool IsTopologyActive,
    bool IsSellable,
    long ConfigurationVersion,
    long UnitVersion);

public sealed record ReservationInventoryBlockWriteModel(
    string ScopeId,
    Guid BlockId,
    Guid PropertyId,
    Guid InventoryUnitId,
    DateOnly Arrival,
    DateOnly Departure,
    ManualInventoryBlockStatus Status,
    long Version);

public sealed record ReservationInventoryAllocationWriteModel(
    string ScopeId,
    Guid AllocationId,
    Guid ReservationId,
    Guid PropertyId,
    DateOnly Arrival,
    DateOnly Departure,
    InventoryAllocationStatus Status,
    IReadOnlyCollection<Guid> InventoryUnitIds,
    long Version);

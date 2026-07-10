namespace Reservations.Contracts;

using Inventory.Contracts;

public sealed record ReservationDto(
    Guid ReservationId,
    Guid PropertyId,
    DateOnly Arrival,
    DateOnly Departure,
    IReadOnlyCollection<Guid> InventoryUnitIds,
    string PrimaryGuestName,
    string? Email,
    string? Phone,
    int GuestCount,
    ReservationSourceKind SourceKind,
    string? SourceSystem,
    string? SourceReference,
    string? Notes,
    ReservationStatus Status,
    Guid AllocationRequestId,
    Guid? AllocationId,
    long? AllocationVersion,
    InventoryAllocationRejectionReason? AllocationRejection,
    long Version,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc);

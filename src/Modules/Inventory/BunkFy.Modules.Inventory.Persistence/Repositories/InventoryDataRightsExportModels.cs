namespace BunkFy.Modules.Inventory.Persistence.Repositories;

using BunkFy.Modules.Inventory.Contracts;
using BunkFy.Modules.Inventory.Domain.Aggregates;

internal sealed record InventoryAllocationDataRightsExport(
    Guid AllocationId,
    Guid ReservationId,
    Guid AllocationRequestId,
    Guid PropertyId,
    DateOnly Arrival,
    DateOnly Departure,
    InventoryAllocationState Status,
    InventoryAllocationRejection Rejection,
    long Version,
    Guid? ReleaseRequestId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ReleasedAtUtc,
    IReadOnlyCollection<Guid> InventoryUnitIds);

internal sealed record InventoryAllocationAmendmentDecisionDataRightsExport(
    Guid AmendmentRequestId,
    Guid AllocationId,
    Guid ReservationId,
    Guid PropertyId,
    string RequestFingerprint,
    bool Confirmed,
    InventoryAllocationRejectionReason? RejectionReason,
    long? AllocationVersion,
    DateTimeOffset DecidedAtUtc);

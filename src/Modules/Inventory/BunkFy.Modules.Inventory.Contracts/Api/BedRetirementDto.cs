namespace BunkFy.Modules.Inventory.Contracts;

using BunkFy.Modules.Properties.Contracts;

public sealed record BedRetirementDto(
    Guid TopologyChangeId,
    Guid PropertyId,
    Guid RoomId,
    Guid BedId,
    string Reason,
    string RequestedBy,
    InventoryRetirementStatus Status,
    BedRetirementFinalizationRejectionReason? RejectionReason,
    int ActiveAllocationCount,
    int ActiveManualBlockCount,
    IReadOnlyCollection<Guid> AffectedReservationIds,
    bool AffectedReservationIdsTruncated,
    long Version,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc,
    DateTimeOffset? CompletedAtUtc);

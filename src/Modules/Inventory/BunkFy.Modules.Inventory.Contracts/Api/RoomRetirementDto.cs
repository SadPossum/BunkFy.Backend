namespace BunkFy.Modules.Inventory.Contracts;

using BunkFy.Modules.Properties.Contracts;

public sealed record RoomRetirementDto(
    Guid TopologyChangeId,
    Guid PropertyId,
    Guid RoomId,
    string Reason,
    string RequestedBy,
    InventoryRetirementStatus Status,
    RoomRetirementFinalizationRejectionReason? RejectionReason,
    int ActiveAllocationCount,
    int ActiveManualBlockCount,
    int ActiveBedRetirementCount,
    IReadOnlyCollection<Guid> AffectedReservationIds,
    bool AffectedReservationIdsTruncated,
    long Version,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc,
    DateTimeOffset? CompletedAtUtc);

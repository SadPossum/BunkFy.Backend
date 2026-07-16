namespace BunkFy.Modules.Inventory.Contracts;

public sealed record RoomInventoryChangeImpactDto(
    Guid PropertyId,
    Guid RoomId,
    int ActiveAllocationCount,
    int ActiveManualBlockCount,
    int ActiveBedRetirementCount,
    int ActiveRoomRetirementCount,
    IReadOnlyCollection<Guid> AffectedReservationIds,
    bool AffectedReservationIdsTruncated,
    bool CanChangeSalesMode);

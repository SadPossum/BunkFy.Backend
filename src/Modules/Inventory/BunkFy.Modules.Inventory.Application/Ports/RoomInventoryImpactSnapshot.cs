namespace BunkFy.Modules.Inventory.Application.Ports;

public sealed record RoomInventoryImpactSnapshot(
    int ActiveAllocationCount,
    int ActiveManualBlockCount,
    int ActiveBedRetirementCount,
    int ActiveRoomRetirementCount,
    IReadOnlyCollection<Guid> AffectedReservationIds,
    bool AffectedReservationIdsTruncated)
{
    public bool PreventsSalesModeChange =>
        this.ActiveAllocationCount > 0 ||
        this.ActiveManualBlockCount > 0 ||
        this.ActiveBedRetirementCount > 0 ||
        this.ActiveRoomRetirementCount > 0;

    public bool PreventsRoomRetirementFinalization =>
        this.ActiveAllocationCount > 0 ||
        this.ActiveManualBlockCount > 0 ||
        this.ActiveBedRetirementCount > 0;
}

namespace BunkFy.Modules.Inventory.Application.Ports;

public sealed record BedRetirementImpactSnapshot(
    int ActiveAllocationCount,
    int ActiveManualBlockCount,
    IReadOnlyCollection<Guid> AffectedReservationIds,
    bool AffectedReservationIdsTruncated)
{
    public bool HasActiveClaims => this.ActiveAllocationCount > 0 || this.ActiveManualBlockCount > 0;
}

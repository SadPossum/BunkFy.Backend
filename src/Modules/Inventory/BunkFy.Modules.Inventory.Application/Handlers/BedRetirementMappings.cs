namespace BunkFy.Modules.Inventory.Application.Handlers;

using BunkFy.Modules.Inventory.Application.Ports;
using BunkFy.Modules.Inventory.Contracts;
using BunkFy.Modules.Inventory.Domain.Aggregates;
using BunkFy.Modules.Properties.Contracts;

internal static class BedRetirementMappings
{
    public static BedRetirementDto ToDto(
        this BedRetirementProcess process,
        BedRetirementImpactSnapshot impact) => new(
        process.Id,
        process.PropertyId,
        process.RoomId,
        process.BedId,
        process.Reason,
        process.RequestedBy,
        (InventoryRetirementStatus)(int)process.State,
        process.RejectionReasonCode.HasValue
            ? (BedRetirementFinalizationRejectionReason)process.RejectionReasonCode.Value
            : null,
        impact.ActiveAllocationCount,
        impact.ActiveManualBlockCount,
        impact.AffectedReservationIds,
        impact.AffectedReservationIdsTruncated,
        process.Version,
        process.CreatedAtUtc,
        process.UpdatedAtUtc,
        process.CompletedAtUtc);
}

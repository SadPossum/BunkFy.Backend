namespace BunkFy.Modules.Inventory.Application.Handlers;

using BunkFy.Modules.Inventory.Application.Ports;
using BunkFy.Modules.Inventory.Contracts;
using BunkFy.Modules.Inventory.Domain.Aggregates;
using BunkFy.Modules.Properties.Contracts;

internal static class RoomRetirementMappings
{
    public static RoomRetirementDto ToDto(
        this RoomRetirementProcess process,
        RoomInventoryImpactSnapshot impact) => new(
        process.Id,
        process.PropertyId,
        process.RoomId,
        process.Reason,
        process.RequestedBy,
        (InventoryRetirementStatus)(int)process.State,
        process.RejectionReasonCode.HasValue
            ? (RoomRetirementFinalizationRejectionReason)process.RejectionReasonCode.Value
            : null,
        impact.ActiveAllocationCount,
        impact.ActiveManualBlockCount,
        impact.ActiveBedRetirementCount,
        impact.AffectedReservationIds,
        impact.AffectedReservationIdsTruncated,
        process.Version,
        process.CreatedAtUtc,
        process.UpdatedAtUtc,
        process.CompletedAtUtc);
}

namespace BunkFy.Modules.Staff.Application.Mapping;

using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Staff.Domain.DataRights;
using BunkFy.Modules.Staff.Domain.Models;

internal static class StaffProcessingRestrictionMappings
{
    public static StaffProcessingRestrictionReceiptDto ToDto(
        this StaffProcessingRestrictionReceipt receipt) => new(
        receipt.Id,
        receipt.RestrictionId,
        ToDto(receipt.Action),
        receipt.StaffMemberId,
        receipt.CaseId,
        receipt.ApprovalRevision,
        receipt.SelectedStaffVersion,
        receipt.ResultingRestrictionVersion,
        receipt.ResultingProjectionRevision,
        receipt.EffectiveRestricted,
        receipt.ActorId,
        receipt.EventId,
        receipt.CompletedAtUtc);

    private static StaffProcessingRestrictionActionDto ToDto(
        StaffProcessingRestrictionAction action) => action switch
        {
            StaffProcessingRestrictionAction.Apply =>
                StaffProcessingRestrictionActionDto.Apply,
            StaffProcessingRestrictionAction.Release =>
                StaffProcessingRestrictionActionDto.Release,
            _ => StaffProcessingRestrictionActionDto.Unknown
        };
}

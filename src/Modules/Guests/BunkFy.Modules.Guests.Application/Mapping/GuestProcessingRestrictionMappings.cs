namespace BunkFy.Modules.Guests.Application.Mapping;

using BunkFy.Modules.Guests.Contracts;
using BunkFy.Modules.Guests.Domain.DataRights;
using BunkFy.Modules.Guests.Domain.Models;

internal static class GuestProcessingRestrictionMappings
{
    public static GuestProcessingRestrictionReceiptDto ToDto(
        this GuestProcessingRestrictionReceipt receipt) => new(
        receipt.Id,
        receipt.RestrictionId,
        ToDto(receipt.Action),
        receipt.PropertyId,
        receipt.GuestId,
        receipt.CaseId,
        receipt.ApprovalRevision,
        receipt.SelectedGuestVersion,
        receipt.ResultingRestrictionVersion,
        receipt.ResultingProjectionRevision,
        receipt.EffectiveRestricted,
        receipt.ActorId,
        receipt.EventId,
        receipt.CompletedAtUtc);

    public static GuestProcessingRestrictionDto ToDto(
        this GuestProcessingRestriction restriction) => new(
        restriction.Id,
        restriction.GuestId,
        restriction.ApplyCaseId,
        restriction.ApplyApprovalRevision,
        restriction.ApplySelectedGuestVersion,
        restriction.Version,
        restriction.AppliedBy,
        restriction.AppliedAtUtc);

    private static GuestProcessingRestrictionActionDto ToDto(
        GuestProcessingRestrictionAction action) => action switch
        {
            GuestProcessingRestrictionAction.Apply =>
                GuestProcessingRestrictionActionDto.Apply,
            GuestProcessingRestrictionAction.Release =>
                GuestProcessingRestrictionActionDto.Release,
            _ => GuestProcessingRestrictionActionDto.Unknown
        };
}

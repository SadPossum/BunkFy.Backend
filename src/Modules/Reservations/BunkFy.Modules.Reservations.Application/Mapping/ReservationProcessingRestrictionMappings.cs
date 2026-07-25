namespace BunkFy.Modules.Reservations.Application.Mapping;

using BunkFy.Modules.Reservations.Contracts;
using BunkFy.Modules.Reservations.Domain.DataRights;
using BunkFy.Modules.Reservations.Domain.Models;

internal static class ReservationProcessingRestrictionMappings
{
    public static ReservationProcessingRestrictionReceiptDto ToDto(
        this ReservationProcessingRestrictionReceipt receipt) => new(
        receipt.Id,
        receipt.RestrictionId,
        ToDto(receipt.Action),
        receipt.PropertyId,
        receipt.ReservationId,
        receipt.CaseId,
        receipt.ApprovalRevision,
        receipt.SelectedReservationVersion,
        receipt.ContractVersion,
        receipt.ResultingRestrictionVersion,
        receipt.ResultingProjectionRevision,
        receipt.EffectiveRestricted,
        receipt.EventId,
        receipt.CompletedAtUtc);

    private static ReservationProcessingRestrictionActionDto ToDto(
        ReservationProcessingRestrictionAction action) => action switch
        {
            ReservationProcessingRestrictionAction.Apply =>
                ReservationProcessingRestrictionActionDto.Apply,
            ReservationProcessingRestrictionAction.Release =>
                ReservationProcessingRestrictionActionDto.Release,
            _ => ReservationProcessingRestrictionActionDto.Unknown
        };
}

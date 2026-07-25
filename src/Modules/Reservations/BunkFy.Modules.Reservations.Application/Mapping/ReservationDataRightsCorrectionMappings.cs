namespace BunkFy.Modules.Reservations.Application.Mapping;

using BunkFy.Modules.Reservations.Contracts;
using BunkFy.Modules.Reservations.Domain.Aggregates;
using BunkFy.Modules.Reservations.Domain.DataRights;

internal static class ReservationDataRightsCorrectionMappings
{
    public static ReservationDataRightsCorrectionReceiptDto ToDto(
        this ReservationDataRightsCorrectionReceipt receipt) => new(
        receipt.ContractVersion,
        receipt.Id,
        receipt.CaseId,
        receipt.ApprovalRevision,
        receipt.ReservationId,
        receipt.SelectedRecordVersion,
        receipt.CurrentRecordVersion,
        receipt.SelectedDetailsRevision,
        receipt.CurrentDetailsRevision,
        receipt.ChangedFields.Select(ToFieldKey).ToArray(),
        receipt.DetailsChangeEventId,
        receipt.EventId,
        receipt.CompletedAtUtc);

    public static string ToFieldKey(this ReservationDetailsField field) => field switch
    {
        ReservationDetailsField.PrimaryGuestName => ReservationDataRightsFieldKeys.PrimaryGuestName,
        ReservationDetailsField.Email => ReservationDataRightsFieldKeys.Email,
        ReservationDetailsField.Phone => ReservationDataRightsFieldKeys.Phone,
        ReservationDetailsField.GuestCount => ReservationDataRightsFieldKeys.GuestCount,
        ReservationDetailsField.Notes => ReservationDataRightsFieldKeys.Notes,
        ReservationDetailsField.ExpectedArrivalTime =>
            ReservationDataRightsFieldKeys.ExpectedArrivalTime,
        ReservationDetailsField.ExpectedDepartureTime =>
            ReservationDataRightsFieldKeys.ExpectedDepartureTime,
        _ => throw new InvalidOperationException(
            "The correction receipt contains an unknown reservation field.")
    };
}

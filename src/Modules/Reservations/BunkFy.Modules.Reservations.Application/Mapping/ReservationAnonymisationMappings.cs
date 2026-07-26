namespace BunkFy.Modules.Reservations.Application.Mapping;

using BunkFy.Modules.Reservations.Contracts;
using BunkFy.Modules.Reservations.Domain.DataRights;
using ContractDisposition =
    BunkFy.Modules.Reservations.Contracts.ReservationAnonymisationDisposition;
using ContractReason =
    BunkFy.Modules.Reservations.Contracts.ReservationAnonymisationReason;

internal static class ReservationAnonymisationMappings
{
    public static ReservationAnonymisationReceiptDto ToDto(
        this ReservationAnonymisationReceipt receipt) => new(
        receipt.ContractVersion,
        receipt.Id,
        receipt.IdempotencyKey,
        receipt.PropertyId,
        receipt.CaseId,
        receipt.ApprovalRevision,
        receipt.OperationRevision,
        receipt.ReservationId,
        receipt.SelectedReservationVersion,
        receipt.ResultingReservationVersion,
        receipt.SelectedDetailsRevision,
        receipt.ResultingDetailsRevision,
        (ContractDisposition)receipt.Disposition,
        (ContractReason)receipt.Reason,
        receipt.RedactedHistoryCount,
        receipt.RemovedGuestLinkCount,
        receipt.ReducedExternalOperationCount,
        receipt.SuppressedReminderCount,
        receipt.ApprovalEvidenceSha256,
        receipt.PolicyEvidenceSha256,
        receipt.EventId,
        receipt.ActorId,
        receipt.CompletedAtUtc,
        receipt.CanonicalSha256);
}

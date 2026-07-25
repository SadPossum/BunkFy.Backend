namespace BunkFy.Modules.Guests.Application.Mapping;

using BunkFy.Modules.Guests.Contracts;
using BunkFy.Modules.Guests.Domain.DataRights;

internal static class GuestAnonymisationMappings
{
    public static GuestAnonymisationReceiptDto ToDto(
        this GuestAnonymisationReceipt receipt) => new(
        receipt.ContractVersion,
        receipt.Id,
        receipt.IdempotencyKey,
        receipt.RoutingPropertyId,
        receipt.CaseId,
        receipt.ApprovalRevision,
        receipt.OperationRevision,
        receipt.GuestId,
        receipt.SelectedGuestVersion,
        receipt.ResultingGuestVersion,
        (GuestAnonymisationDisposition)receipt.Disposition,
        (GuestAnonymisationReason)receipt.Reason,
        receipt.AffectedPropertyCount,
        receipt.ApprovalEvidenceSha256,
        receipt.PolicySetSha256,
        receipt.EventId,
        receipt.ActorId,
        receipt.CompletedAtUtc,
        receipt.CanonicalSha256);
}

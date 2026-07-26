namespace BunkFy.Modules.Ingestion.Application.DataRights;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Ingestion.Contracts;
using BunkFy.Modules.Ingestion.Domain.DataRights;

internal static class IngestionAnonymisationRestoreRequestValidator
{
    public static bool IsValid(
        DataRightsAnonymisationRestoreRequest? request,
        string tenantId) =>
        request is not null &&
        request.ContractVersion ==
            DataRightsAnonymisationRestoreContract.CurrentVersion &&
        string.Equals(
            request.TenantId,
            tenantId,
            StringComparison.Ordinal) &&
        request.LedgerEntryId != Guid.Empty &&
        request.TenantSequence > 0 &&
        IsSha256(request.LedgerEntrySha256) &&
        request.RoutingPropertyId != Guid.Empty &&
        string.Equals(
            request.OwnerKey,
            IngestionDataRightsCoordinates.Owner,
            StringComparison.Ordinal) &&
        string.Equals(
            request.RecordType,
            IngestionDataRightsCoordinates.ReservationSourceLinkRecordType,
            StringComparison.Ordinal) &&
        request.RecordId != Guid.Empty &&
        request.OwnerReceiptContractVersion > 0 &&
        request.OwnerReceiptId != Guid.Empty &&
        IsSha256(request.OwnerReceiptSha256) &&
        request.ResultingRecordVersion > 1 &&
        request.OriginallyCompletedAtUtc != default &&
        request.OriginallyCompletedAtUtc.Offset == TimeSpan.Zero;

    private static bool IsSha256(string? value) =>
        value is
        {
            Length: IngestionAnonymisationTombstone.Sha256Length
        } &&
        value.All(character =>
            character is (>= '0' and <= '9') or (>= 'a' and <= 'f'));
}

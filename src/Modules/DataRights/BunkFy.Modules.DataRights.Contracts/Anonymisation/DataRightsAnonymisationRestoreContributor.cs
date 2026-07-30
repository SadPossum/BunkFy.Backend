namespace BunkFy.Modules.DataRights.Contracts;

public interface IDataRightsAnonymisationRestoreContributor
{
    string OwnerKey { get; }

    string RecordType { get; }

    int ContractVersion { get; }

    Task<DataRightsAnonymisationRestoreResult> RestoreAsync(
        DataRightsAnonymisationRestoreRequest request,
        CancellationToken cancellationToken);
}

public sealed record DataRightsAnonymisationRestoreRequest(
    int ContractVersion,
    string TenantId,
    Guid LedgerEntryId,
    long TenantSequence,
    string LedgerEntrySha256,
    Guid RoutingPropertyId,
    string OwnerKey,
    string RecordType,
    Guid RecordId,
    int OwnerReceiptContractVersion,
    Guid OwnerReceiptId,
    string OwnerReceiptSha256,
    long? ResultingRecordVersion,
    DateTimeOffset OriginallyCompletedAtUtc);

public sealed record DataRightsAnonymisationRestoreProof(
    Guid LedgerEntryId,
    Guid OwnerReceiptId,
    string OwnerReceiptSha256,
    long ResultingRecordVersion,
    long TombstoneRevision,
    DateTimeOffset ReplayedAtUtc);

public sealed record DataRightsAnonymisationRestoreResult(
    int ContractVersion,
    DataRightsAnonymisationRestoreStatus Status,
    DataRightsAnonymisationRestoreProof? Proof,
    string? OutcomeCode)
{
    public static DataRightsAnonymisationRestoreResult Completed(
        DataRightsAnonymisationRestoreProof proof) =>
        Completed(
            DataRightsAnonymisationRestoreContract.CurrentVersion,
            proof);

    public static DataRightsAnonymisationRestoreResult Completed(
        int contractVersion,
        DataRightsAnonymisationRestoreProof proof) =>
        new(
            contractVersion,
            DataRightsAnonymisationRestoreStatus.Completed,
            proof,
            OutcomeCode: null);

    public static DataRightsAnonymisationRestoreResult Failed(string failureCode) =>
        Failed(
            DataRightsAnonymisationRestoreContract.CurrentVersion,
            failureCode);

    public static DataRightsAnonymisationRestoreResult Failed(
        int contractVersion,
        string failureCode) =>
        new(
            contractVersion,
            DataRightsAnonymisationRestoreStatus.Failed,
            Proof: null,
            failureCode);
}

public enum DataRightsAnonymisationRestoreStatus
{
    Unknown = 0,
    Completed = 1,
    Failed = 2
}

public static class DataRightsAnonymisationRestoreContract
{
    public const int CurrentVersion = 2;
}

namespace BunkFy.Modules.DataRights.Contracts;

public interface IDataRightsAnonymisationRestorePrerequisiteV3
{
    string OwnerKey { get; }

    string RecordType { get; }

    DataRightsCaseType CaseType { get; }

    int ContractVersion { get; }

    Task<DataRightsAnonymisationRestorePrerequisiteResult> ExecuteAsync(
        DataRightsAnonymisationRestoreRequestV3 request,
        CancellationToken cancellationToken);
}

public interface IDataRightsAnonymisationRestoreContributorV3
{
    string OwnerKey { get; }

    string RecordType { get; }

    DataRightsCaseType CaseType { get; }

    int ContractVersion { get; }

    Task<DataRightsAnonymisationRestoreResult> RestoreAsync(
        DataRightsAnonymisationRestoreRequestV3 request,
        CancellationToken cancellationToken);
}

public sealed record DataRightsAnonymisationRestoreRequestV3(
    int ContractVersion,
    string TenantId,
    Guid LedgerEntryId,
    long TenantSequence,
    string LedgerEntrySha256,
    DataRightsCaseType CaseType,
    DataRightsExecutionScopeKind ScopeKind,
    Guid? RoutingPropertyId,
    string OwnerKey,
    string RecordType,
    Guid RecordId,
    int OwnerReceiptContractVersion,
    Guid OwnerReceiptId,
    string OwnerReceiptSha256,
    long ResultingRecordVersion,
    DateTimeOffset OriginallyCompletedAtUtc);

public sealed record DataRightsAnonymisationRestorePrerequisiteResult(
    int ContractVersion,
    DataRightsAnonymisationRestorePrerequisiteStatus Status,
    string? OutcomeCode)
{
    public static DataRightsAnonymisationRestorePrerequisiteResult Completed(
        int contractVersion) =>
        new(
            contractVersion,
            DataRightsAnonymisationRestorePrerequisiteStatus.Completed,
            OutcomeCode: null);

    public static DataRightsAnonymisationRestorePrerequisiteResult Blocked(
        int contractVersion,
        string blockerCode) =>
        new(
            contractVersion,
            DataRightsAnonymisationRestorePrerequisiteStatus.Blocked,
            blockerCode);

    public static DataRightsAnonymisationRestorePrerequisiteResult
        RetryRequired(
            int contractVersion,
            string retryCode) =>
        new(
            contractVersion,
            DataRightsAnonymisationRestorePrerequisiteStatus.RetryRequired,
            retryCode);
}

public enum DataRightsAnonymisationRestorePrerequisiteStatus
{
    Unknown = 0,
    Completed = 1,
    Blocked = 2,
    RetryRequired = 3
}

public static class DataRightsAnonymisationRestoreContractV3
{
    public const int CurrentVersion = 3;
}

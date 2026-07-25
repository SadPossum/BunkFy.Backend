namespace BunkFy.Modules.DataRights.Contracts;

public interface IDataRightsAnonymisationContributor
{
    string OwnerKey { get; }

    int ContractVersion { get; }

    Task<DataRightsAnonymisationContributionResult> ExecuteAsync(
        DataRightsAnonymisationContributionRequest request,
        CancellationToken cancellationToken);
}

public sealed record DataRightsAnonymisationContributionRequest(
    int ContractVersion,
    string TenantId,
    Guid WorkItemId,
    Guid IdempotencyKey,
    Guid RoutingPropertyId,
    Guid CaseId,
    long ApprovalRevision,
    long OperationRevision,
    DataRightsSubjectCoordinate Coordinate,
    DataRightsApprovalEvidence RoutingPolicy,
    string ExecutingActorId,
    DateTimeOffset DeadlineUtc);

public sealed record DataRightsAnonymisationOwnerProof(
    int ReceiptContractVersion,
    Guid ReceiptId,
    long ResultingRecordVersion,
    string DispositionCode,
    string ReasonCode,
    string ReceiptSha256,
    DateTimeOffset CompletedAtUtc);

public sealed record DataRightsAnonymisationContributionResult(
    int ContractVersion,
    DataRightsAnonymisationContributionStatus Status,
    DataRightsAnonymisationOwnerProof? OwnerProof,
    string? OutcomeCode)
{
    public static DataRightsAnonymisationContributionResult Completed(
        DataRightsAnonymisationOwnerProof ownerProof) =>
        new(
            DataRightsAnonymisationContract.CurrentVersion,
            DataRightsAnonymisationContributionStatus.Completed,
            ownerProof,
            OutcomeCode: null);

    public static DataRightsAnonymisationContributionResult Blocked(string blockerCode) =>
        new(
            DataRightsAnonymisationContract.CurrentVersion,
            DataRightsAnonymisationContributionStatus.Blocked,
            OwnerProof: null,
            blockerCode);

    public static DataRightsAnonymisationContributionResult Failed(string failureCode) =>
        new(
            DataRightsAnonymisationContract.CurrentVersion,
            DataRightsAnonymisationContributionStatus.Failed,
            OwnerProof: null,
            failureCode);
}

public enum DataRightsAnonymisationContributionStatus
{
    Unknown = 0,
    Completed = 1,
    Blocked = 2,
    Failed = 3
}

public static class DataRightsAnonymisationContract
{
    public const int CurrentVersion = 1;
    public const int CodeMaxLength = 200;
    public const int Sha256Length = 64;
}

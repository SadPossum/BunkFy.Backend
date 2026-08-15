namespace BunkFy.Modules.DataRights.Contracts;

public interface IDataRightsRestrictionContributor
{
    string OwnerKey { get; }

    int ContractVersion { get; }

    Task<DataRightsRestrictionContributionResult> ExecuteAsync(
        DataRightsRestrictionContributionRequest request,
        CancellationToken cancellationToken);
}

public sealed record DataRightsRestrictionContributionRequest(
    int ContractVersion,
    string TenantId,
    Guid IdempotencyKey,
    Guid? PropertyId,
    Guid CaseId,
    long ApprovalRevision,
    DataRightsSubjectCoordinate Coordinate,
    DataRightsRestrictionDirective Directive,
    string ExecutingActorId,
    DateTimeOffset DeadlineUtc,
    DataRightsCaseType CaseType = DataRightsCaseType.GuestRights);

public sealed record DataRightsRestrictionOwnerProof(
    int ReceiptContractVersion,
    Guid ReceiptId,
    Guid OwnerOperationId,
    long ResultingOwnerRevision,
    long ResultingProjectionRevision,
    bool EffectiveRestricted,
    string ReceiptSha256,
    DateTimeOffset CompletedAtUtc);

public sealed record DataRightsRestrictionContributionResult(
    int ContractVersion,
    DataRightsRestrictionContributionStatus Status,
    DataRightsRestrictionOwnerProof? OwnerProof,
    string? OutcomeCode)
{
    public static DataRightsRestrictionContributionResult Completed(
        DataRightsRestrictionOwnerProof ownerProof) =>
        new(
            DataRightsRestrictionContract.CurrentVersion,
            DataRightsRestrictionContributionStatus.Completed,
            ownerProof,
            OutcomeCode: null);

    public static DataRightsRestrictionContributionResult Blocked(string blockerCode) =>
        new(
            DataRightsRestrictionContract.CurrentVersion,
            DataRightsRestrictionContributionStatus.Blocked,
            OwnerProof: null,
            blockerCode);

    public static DataRightsRestrictionContributionResult Failed(string failureCode) =>
        new(
            DataRightsRestrictionContract.CurrentVersion,
            DataRightsRestrictionContributionStatus.Failed,
            OwnerProof: null,
            failureCode);
}

public enum DataRightsRestrictionContributionStatus
{
    Unknown = 0,
    Completed = 1,
    Blocked = 2,
    Failed = 3
}

public static class DataRightsRestrictionContract
{
    public const int CurrentVersion = 2;
    public const int OwnerKeyMaxLength = 100;
    public const int CodeMaxLength = 200;
    public const int Sha256Length = 64;
}

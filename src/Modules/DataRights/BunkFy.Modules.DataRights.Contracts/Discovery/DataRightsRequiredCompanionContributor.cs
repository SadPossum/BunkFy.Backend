namespace BunkFy.Modules.DataRights.Contracts;

public interface IDataRightsRequiredCompanionContributor
{
    string ContributorKey { get; }

    string SourceOwnerKey { get; }

    string SourceRecordType { get; }

    DataRightsCaseType CaseType { get; }

    DataRightsOperation Operation { get; }

    int ContractVersion { get; }

    Task<DataRightsRequiredCompanionResult> ExpandAsync(
        DataRightsRequiredCompanionRequest request,
        CancellationToken cancellationToken);
}

public sealed record DataRightsRequiredCompanionRequest(
    int ContractVersion,
    string TenantId,
    DataRightsCaseType CaseType,
    DataRightsOperation Operation,
    Guid? PropertyId,
    Guid CaseId,
    DataRightsSubjectCoordinate SourceCoordinate,
    int RemainingSubjectCapacity);

public sealed record DataRightsRequiredCompanionResult(
    int ContractVersion,
    DataRightsRequiredCompanionStatus Status,
    IReadOnlyCollection<DataRightsSubjectCoordinate> Coordinates,
    string? OutcomeCode)
{
    public static DataRightsRequiredCompanionResult Completed(
        IReadOnlyCollection<DataRightsSubjectCoordinate>? coordinates = null) =>
        new(
            DataRightsRequiredCompanionContract.CurrentVersion,
            DataRightsRequiredCompanionStatus.Completed,
            coordinates ?? [],
            OutcomeCode: null);

    public static DataRightsRequiredCompanionResult Blocked(
        string outcomeCode) =>
        new(
            DataRightsRequiredCompanionContract.CurrentVersion,
            DataRightsRequiredCompanionStatus.Blocked,
            [],
            outcomeCode);

    public static DataRightsRequiredCompanionResult RetryRequired(
        string outcomeCode) =>
        new(
            DataRightsRequiredCompanionContract.CurrentVersion,
            DataRightsRequiredCompanionStatus.RetryRequired,
            [],
            outcomeCode);
}

public enum DataRightsRequiredCompanionStatus
{
    Unknown = 0,
    Completed = 1,
    Blocked = 2,
    RetryRequired = 3
}

public static class DataRightsRequiredCompanionContract
{
    public const int CurrentVersion = 1;
    public const int MaximumContributorsPerCoordinate = 16;
    public const int MaximumCoordinatesPerContribution = 32;
    public const int ContributorKeyMaxLength = 100;
    public const int OutcomeCodeMaxLength = 200;
}

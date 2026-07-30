namespace BunkFy.Modules.DataRights.Contracts;

public interface IDataRightsAnonymisationExecutionPrerequisiteV2
{
    string OwnerKey { get; }

    string RecordType { get; }

    DataRightsCaseType CaseType { get; }

    int ContractVersion { get; }

    Task<DataRightsAnonymisationExecutionPrerequisiteResult> ExecuteAsync(
        DataRightsAnonymisationContributionRequestV2 request,
        CancellationToken cancellationToken);
}

public sealed record DataRightsAnonymisationExecutionPrerequisiteResult(
    int ContractVersion,
    DataRightsAnonymisationExecutionPrerequisiteStatus Status,
    string? OutcomeCode)
{
    public static DataRightsAnonymisationExecutionPrerequisiteResult Completed(
        int contractVersion) =>
        new(
            contractVersion,
            DataRightsAnonymisationExecutionPrerequisiteStatus.Completed,
            OutcomeCode: null);

    public static DataRightsAnonymisationExecutionPrerequisiteResult Blocked(
        int contractVersion,
        string blockerCode) =>
        new(
            contractVersion,
            DataRightsAnonymisationExecutionPrerequisiteStatus.Blocked,
            blockerCode);

    public static DataRightsAnonymisationExecutionPrerequisiteResult
        RetryRequired(
            int contractVersion,
            string retryCode) =>
        new(
            contractVersion,
            DataRightsAnonymisationExecutionPrerequisiteStatus.RetryRequired,
            retryCode);
}

public enum DataRightsAnonymisationExecutionPrerequisiteStatus
{
    Unknown = 0,
    Completed = 1,
    Blocked = 2,
    RetryRequired = 3
}

public static class DataRightsAnonymisationExecutionPrerequisiteContractV2
{
    public const int CurrentVersion = 2;
}

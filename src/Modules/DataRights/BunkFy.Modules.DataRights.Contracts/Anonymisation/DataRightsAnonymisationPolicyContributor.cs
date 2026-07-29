namespace BunkFy.Modules.DataRights.Contracts;

public interface IDataRightsAnonymisationPolicyContributor
{
    int ContractVersion { get; }

    DataRightsCaseType CaseType { get; }

    string OwnerKey { get; }

    string RecordType { get; }

    Task<DataRightsAnonymisationPolicyContributionResult> EvaluateAsync(
        DataRightsAnonymisationPolicyContributionRequest request,
        CancellationToken cancellationToken);
}

public sealed record DataRightsAnonymisationPolicyContributionRequest(
    int ContractVersion,
    string TenantId,
    DataRightsCaseType CaseType,
    Guid? PropertyId,
    Guid CaseId,
    DataRightsSubjectCoordinate Coordinate);

public sealed record DataRightsAnonymisationPolicyContributionEvidence(
    string OperatingCountryCode,
    string PolicyId,
    int PolicyVersion,
    string RetentionPolicyId,
    int RetentionPolicyVersion,
    string ContentSha256,
    string PurposeCode,
    string Surface,
    string SourceProvenance,
    string RetentionDataClass,
    string RetentionTrigger,
    DateTimeOffset RetentionTriggeredAtUtc,
    DateTimeOffset RetentionDeadlineUtc,
    DateTimeOffset EvaluatedAtUtc,
    IReadOnlyCollection<DataRightsApprovalEvidenceBinding> StateBindings,
    bool RequiresDistinctExecutor);

public sealed record DataRightsApprovalEvidenceBinding(
    string Key,
    long Version,
    string Sha256);

public sealed record DataRightsAnonymisationPolicyContributionResult(
    int ContractVersion,
    DataRightsAnonymisationPolicyContributionStatus Status,
    DataRightsAnonymisationPolicyContributionEvidence? Evidence,
    string? OutcomeCode)
{
    public static DataRightsAnonymisationPolicyContributionResult Approved(
        DataRightsAnonymisationPolicyContributionEvidence evidence) =>
        new(
            DataRightsAnonymisationPolicyContract.CurrentVersion,
            DataRightsAnonymisationPolicyContributionStatus.Approved,
            evidence,
            OutcomeCode: null);

    public static DataRightsAnonymisationPolicyContributionResult Denied(
        string outcomeCode) =>
        new(
            DataRightsAnonymisationPolicyContract.CurrentVersion,
            DataRightsAnonymisationPolicyContributionStatus.Denied,
            Evidence: null,
            outcomeCode);
}

public enum DataRightsAnonymisationPolicyContributionStatus
{
    Unknown = 0,
    Approved = 1,
    Denied = 2
}

public static class DataRightsAnonymisationPolicyContract
{
    public const int CurrentVersion = 1;
    public const int MaximumStateBindings = 8;
    public const int KeyMaxLength = 128;
    public const int OutcomeCodeMaxLength = 200;
    public const int Sha256Length = 64;
}

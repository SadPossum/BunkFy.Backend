namespace BunkFy.Modules.DataRights.Contracts;

public interface IDataRightsAnonymisationContributorV2
{
    string OwnerKey { get; }

    string RecordType { get; }

    DataRightsCaseType CaseType { get; }

    int ContractVersion { get; }

    Task<DataRightsAnonymisationContributionResult> ExecuteAsync(
        DataRightsAnonymisationContributionRequestV2 request,
        CancellationToken cancellationToken);
}

public sealed record DataRightsAnonymisationContributionRequestV2(
    int ContractVersion,
    string TenantId,
    DataRightsCaseType CaseType,
    DataRightsExecutionScopeKind ScopeKind,
    Guid? PropertyId,
    Guid WorkItemId,
    Guid IdempotencyKey,
    Guid CaseId,
    long ApprovalRevision,
    long OperationRevision,
    DataRightsSubjectCoordinate Coordinate,
    DataRightsApprovalEvidence ApprovalEvidence,
    string ExecutingActorId,
    DateTimeOffset DeadlineUtc);

public static class DataRightsAnonymisationContractV2
{
    public const int CurrentVersion = 2;
}

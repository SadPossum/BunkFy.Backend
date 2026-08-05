namespace BunkFy.Modules.DataRights.Contracts;

public interface ITenantTerminationContributor
{
    TenantTerminationContributorDescriptor Descriptor { get; }

    Task<TenantTerminationContributionResult> ExecuteAsync(
        TenantTerminationContributionRequest request,
        CancellationToken cancellationToken);
}

public sealed record TenantTerminationContributorDescriptor(
    string OwnerKey,
    int ContractVersion,
    IReadOnlyCollection<TenantTerminationContributorPhasePlan> PhasePlans,
    bool MandatoryForProduction,
    int CatalogVersion,
    string CatalogSha256);

public sealed record TenantTerminationContributorPhasePlan(
    TenantTerminationContributionPhase Phase,
    IReadOnlyCollection<string> DependsOnOwnerKeys,
    TenantTerminationExecutionBoundary ExecutionBoundary =
        TenantTerminationExecutionBoundary.TenantScopedTask);

public sealed record TenantTerminationContributionRequest(
    int ContractVersion,
    string TenantId,
    Guid ProcessId,
    Guid CaseId,
    long ApprovalRevision,
    long OperationRevision,
    Guid TerminationEpoch,
    TenantTerminationContributionPhase Phase,
    Guid WorkItemId,
    Guid IdempotencyKey,
    string PolicyEvidenceSha256,
    string ExecutingActorId,
    DateTimeOffset DeadlineUtc);

public sealed record TenantTerminationContributionResult(
    TenantTerminationContributionStatus Status,
    string ResultCode,
    long AffectedCount,
    long RetainedMinimumCount,
    long RemainingActiveCount,
    DateTimeOffset? HoldReviewAtUtc,
    long? SelectedProofRevision,
    long? ResultingProofRevision,
    int CatalogVersion,
    string CatalogSha256,
    DateTimeOffset RecordedAtUtc);

public static class TenantTerminationContract
{
    public const int CurrentVersion = 1;
    public const int OwnerKeyMaxLength = 100;
    public const int ResultCodeMaxLength = 200;
    public const int ActorIdMaxLength = 200;
    public const int MaximumDependencies = 32;
    public const int MaximumContributors = 64;
    public const int Sha256Length = 64;
}

public enum TenantTerminationContributionPhase
{
    Unknown = 0,
    Freeze = 1,
    Export = 2,
    Destroy = 3,
    Verify = 4,
    Restore = 5
}

public enum TenantTerminationContributionStatus
{
    Unknown = 0,
    Completed = 1,
    Blocked = 2,
    RetryRequired = 3,
    Failed = 4
}

public enum TenantTerminationExecutionBoundary
{
    Unknown = 0,
    TenantScopedTask = 1,
    GlobalControlTask = 2
}

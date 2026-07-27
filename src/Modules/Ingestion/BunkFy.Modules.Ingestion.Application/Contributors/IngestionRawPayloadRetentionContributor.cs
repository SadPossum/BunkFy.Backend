namespace BunkFy.Modules.Ingestion.Application.Contributors;

using BunkFy.Modules.Retention.Contracts;

internal sealed class IngestionRawPayloadRetentionContributor(
    IngestionRetentionExecutor executor)
    : IRetentionExecutionContributor
{
    public RetentionScheduleDescriptor Schedule { get; } = new(
        IngestionRetentionCoordinates.OwnerKey,
        IngestionRetentionCoordinates.RawPayloadDataClass,
        RetentionTargetScopeKind.Tenant,
        IngestionRetentionCoordinates.ExecutionPolicyVersion,
        TimeSpan.FromHours(1),
        maxAttempts: 3,
        executionTimeout: TimeSpan.FromMinutes(15));

    public Task<RetentionContributionResult> ExecuteAsync(
        RetentionContributionRequest request,
        CancellationToken cancellationToken) =>
        executor.ExecuteRawPayloadsAsync(request, cancellationToken);
}

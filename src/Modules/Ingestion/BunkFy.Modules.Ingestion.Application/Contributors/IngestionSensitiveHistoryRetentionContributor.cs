namespace BunkFy.Modules.Ingestion.Application.Contributors;

using BunkFy.Modules.Retention.Contracts;

internal sealed class IngestionSensitiveHistoryRetentionContributor(
    IngestionRetentionExecutor executor)
    : IRetentionExecutionContributor
{
    public RetentionScheduleDescriptor Schedule { get; } = new(
        IngestionRetentionCoordinates.OwnerKey,
        IngestionRetentionCoordinates.SensitiveHistoryDataClass,
        RetentionTargetScopeKind.Tenant,
        IngestionRetentionCoordinates.ExecutionPolicyVersion,
        TimeSpan.FromHours(6),
        maxAttempts: 3,
        executionTimeout: TimeSpan.FromMinutes(15));

    public Task<RetentionContributionResult> ExecuteAsync(
        RetentionContributionRequest request,
        CancellationToken cancellationToken) =>
        executor.ExecuteSensitiveHistoryAsync(request, cancellationToken);
}

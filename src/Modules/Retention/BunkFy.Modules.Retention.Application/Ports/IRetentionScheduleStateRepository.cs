namespace BunkFy.Modules.Retention.Application.Ports;

using BunkFy.Modules.Retention.Domain.Aggregates;

public interface IRetentionScheduleStateRepository
{
    Task RecordStartedAsync(
        RetentionExecution execution,
        DateTimeOffset nextDueAtUtc,
        CancellationToken cancellationToken);

    Task RecordCompletedAsync(
        RetentionExecution execution,
        CancellationToken cancellationToken);
}

public interface IRetentionScheduleHealthReader
{
    Task<IReadOnlyList<RetentionScheduleStateSnapshot>> ListAsync(
        CancellationToken cancellationToken);
}

public sealed record RetentionScheduleStateSnapshot(
    string OwnerKey,
    string DataClassKey,
    Guid? PropertyId,
    int ExecutionPolicyVersion,
    int State,
    DateTimeOffset LastStartedAtUtc,
    DateTimeOffset? LastCompletedAtUtc,
    DateTimeOffset NextDueAtUtc,
    int ConsecutiveFailures,
    int? LastScannedCount,
    int? LastAffectedCount,
    int? LastRemainingCount,
    string? OutcomeCode,
    DateTimeOffset? HoldReviewDueAtUtc);

namespace BunkFy.Modules.Retention.Application.Handlers;

using BunkFy.Modules.Retention.Application.Ports;
using BunkFy.Modules.Retention.Application.Queries;
using BunkFy.Modules.Retention.Contracts;
using BunkFy.Modules.Retention.Domain.Models;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;

internal sealed class ListRetentionScheduleHealthQueryHandler(
    IRetentionScopeRepository scopes,
    IRetentionScheduleHealthReader states,
    IEnumerable<IRetentionExecutionContributor> contributors,
    ISystemClock clock)
    : IQueryHandler<
        ListRetentionScheduleHealthQuery,
        RetentionScheduleHealthListResponse>
{
    public async Task<Result<RetentionScheduleHealthListResponse>> HandleAsync(
        ListRetentionScheduleHealthQuery query,
        CancellationToken cancellationToken)
    {
        RetentionScheduleDescriptor[] descriptors = contributors
            .Select(contributor => contributor.Schedule)
            .OrderBy(descriptor => descriptor.ContributorKey, StringComparer.Ordinal)
            .ToArray();
        IReadOnlyList<RetentionScheduleStateSnapshot> snapshots =
            await states.ListAsync(cancellationToken).ConfigureAwait(false);
        Dictionary<ScheduleKey, RetentionScheduleStateSnapshot> byCoordinate =
            snapshots.ToDictionary(
                snapshot => new ScheduleKey(
                    snapshot.OwnerKey,
                    snapshot.DataClassKey,
                    snapshot.PropertyId,
                    snapshot.ExecutionPolicyVersion));

        List<RetentionScheduleHealthDto> health = [];
        foreach (IGrouping<RetentionTargetScopeKind, RetentionScheduleDescriptor> group in
                 descriptors.GroupBy(descriptor => descriptor.TargetScopeKind))
        {
            IReadOnlyList<RetentionScheduleTarget> targets =
                await scopes.ListCurrentActiveTargetsAsync(
                    group.Key,
                    cancellationToken).ConfigureAwait(false);
            foreach (RetentionScheduleDescriptor descriptor in group)
            {
                foreach (RetentionScheduleTarget target in targets)
                {
                    byCoordinate.TryGetValue(
                        new(
                            descriptor.OwnerKey,
                            descriptor.DataClassKey,
                            target.PropertyId,
                            descriptor.ExecutionPolicyVersion),
                        out RetentionScheduleStateSnapshot? snapshot);
                    health.Add(ToDto(descriptor, target, snapshot, clock.UtcNow));
                }
            }
        }

        return Result.Success(new RetentionScheduleHealthListResponse(
            health
                .OrderByDescending(item => item.Overdue)
                .ThenBy(item => item.NextDueAtUtc)
                .ThenBy(item => item.OwnerKey, StringComparer.Ordinal)
                .ThenBy(item => item.DataClassKey, StringComparer.Ordinal)
                .ThenBy(item => item.PropertyId)
                .ToArray()));
    }

    private static RetentionScheduleHealthDto ToDto(
        RetentionScheduleDescriptor descriptor,
        RetentionScheduleTarget target,
        RetentionScheduleStateSnapshot? snapshot,
        DateTimeOffset now)
    {
        DateTimeOffset nextDueAtUtc = snapshot?.NextDueAtUtc ?? now;
        return new(
            descriptor.OwnerKey,
            descriptor.DataClassKey,
            descriptor.TargetScopeKind,
            target.PropertyId,
            descriptor.ExecutionPolicyVersion,
            snapshot is null
                ? RetentionExecutionStatus.NeverRun
                : Map(snapshot.State),
            snapshot?.LastStartedAtUtc,
            snapshot?.LastCompletedAtUtc,
            nextDueAtUtc,
            snapshot is not null && nextDueAtUtc < now,
            snapshot?.ConsecutiveFailures ?? 0,
            snapshot?.LastScannedCount,
            snapshot?.LastAffectedCount,
            snapshot?.LastRemainingCount,
            snapshot?.OutcomeCode,
            snapshot?.HoldReviewDueAtUtc);
    }

    private static RetentionExecutionStatus Map(int state) =>
        (RetentionExecutionState)state switch
        {
            RetentionExecutionState.Running => RetentionExecutionStatus.Running,
            RetentionExecutionState.Completed => RetentionExecutionStatus.Completed,
            RetentionExecutionState.Blocked => RetentionExecutionStatus.Blocked,
            RetentionExecutionState.Failed => RetentionExecutionStatus.Failed,
            _ => RetentionExecutionStatus.Unknown
        };

    private sealed record ScheduleKey(
        string OwnerKey,
        string DataClassKey,
        Guid? PropertyId,
        int ExecutionPolicyVersion);
}

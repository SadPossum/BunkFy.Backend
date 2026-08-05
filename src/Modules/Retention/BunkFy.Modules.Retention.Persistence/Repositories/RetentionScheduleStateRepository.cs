namespace BunkFy.Modules.Retention.Persistence.Repositories;

using BunkFy.Modules.Retention.Application.Ports;
using BunkFy.Modules.Retention.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;

internal sealed class RetentionScheduleStateRepository(RetentionDbContext dbContext)
    : IRetentionScheduleStateRepository, IRetentionScheduleHealthReader
{
    public async Task RecordStartedAsync(
        RetentionExecution execution,
        DateTimeOffset nextDueAtUtc,
        CancellationToken cancellationToken)
    {
        RetentionScheduleState? state = await this.GetAsync(
            execution,
            cancellationToken).ConfigureAwait(false);
        if (state is null)
        {
            dbContext.ScheduleStates.Add(new(execution, nextDueAtUtc));
            return;
        }

        state.RecordStarted(execution, nextDueAtUtc);
    }

    public async Task RecordCompletedAsync(
        RetentionExecution execution,
        CancellationToken cancellationToken)
    {
        RetentionScheduleState state = await this.GetAsync(
            execution,
            cancellationToken).ConfigureAwait(false) ??
            throw new InvalidOperationException(
                "Retention.ScheduleStateUnavailable");
        state.RecordCompleted(execution);
    }

    public async Task<IReadOnlyList<RetentionScheduleStateSnapshot>> ListAsync(
        CancellationToken cancellationToken) =>
        await dbContext.ScheduleStates
            .AsNoTracking()
            .OrderBy(state => state.OwnerKey)
            .ThenBy(state => state.DataClassKey)
            .ThenBy(state => state.TargetKey)
            .Select(state => new RetentionScheduleStateSnapshot(
                state.OwnerKey,
                state.DataClassKey,
                state.PropertyId,
                state.ExecutionPolicyVersion,
                (int)state.State,
                state.LastExecutionId,
                state.LastStartedAtUtc,
                state.LastCompletedAtUtc,
                state.NextDueAtUtc,
                state.ConsecutiveFailures,
                state.LastScannedCount,
                state.LastAffectedCount,
                state.LastRemainingCount,
                state.OutcomeCode,
                state.HoldReviewDueAtUtc))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

    private Task<RetentionScheduleState?> GetAsync(
        RetentionExecution execution,
        CancellationToken cancellationToken)
    {
        string targetKey = RetentionScheduleState.CreateTargetKey(
            execution.PropertyId);
        return dbContext.ScheduleStates.SingleOrDefaultAsync(
            state =>
                state.OwnerKey == execution.OwnerKey &&
                state.DataClassKey == execution.DataClassKey &&
                state.TargetKey == targetKey &&
                state.ExecutionPolicyVersion ==
                    execution.ExecutionPolicyVersion,
            cancellationToken);
    }
}

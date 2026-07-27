namespace BunkFy.Modules.Ingestion.Application.Handlers;

using BunkFy.Modules.Ingestion.Application.Commands;
using BunkFy.Modules.Ingestion.Application.Contributors;
using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Domain.Retention;
using BunkFy.Modules.Retention.Contracts;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Scoping;

internal sealed class BeginIngestionRetentionExecutionCommandHandler(
    IIngestionRetentionExecutionRepository executions,
    IScopeContext scopeContext)
    : ICommandHandler<
        BeginIngestionRetentionExecutionCommand,
        IngestionRetentionExecutionStart>
{
    public async Task<Result<IngestionRetentionExecutionStart>> HandleAsync(
        BeginIngestionRetentionExecutionCommand command,
        CancellationToken cancellationToken)
    {
        RetentionContributionRequest request = command.Request;
        if (!scopeContext.IsEnabled ||
            !string.Equals(
                scopeContext.ScopeId,
                request.TenantId,
                StringComparison.Ordinal) ||
            request.ContractVersion != RetentionExecutionContract.CurrentVersion ||
            request.PropertyId is not null ||
            !string.Equals(
                request.OwnerKey,
                IngestionRetentionCoordinates.OwnerKey,
                StringComparison.Ordinal) ||
            !IngestionRetentionCoordinates.IsSupported(request.DataClassKey) ||
            request.ExecutionPolicyVersion !=
                IngestionRetentionCoordinates.ExecutionPolicyVersion)
        {
            return Result.Failure<IngestionRetentionExecutionStart>(
                IngestionRetentionExecutionErrors.CoordinateInvalid);
        }

        IngestionRetentionExecution? execution = await executions.GetAsync(
            request.ExecutionId,
            cancellationToken).ConfigureAwait(false);
        if (execution is null)
        {
            Result<IngestionRetentionExecution> started =
                IngestionRetentionExecution.Start(
                    request.ExecutionId,
                    request.TenantId,
                    request.DataClassKey,
                    request.ExecutionPolicyVersion,
                    request.Attempt,
                    request.StartedAtUtc,
                    request.DeadlineUtc);
            if (started.IsFailure)
            {
                return Result.Failure<IngestionRetentionExecutionStart>(
                    started.Error);
            }

            execution = started.Value;
            await executions.AddAsync(
                execution,
                cancellationToken).ConfigureAwait(false);
        }
        else if (!execution.MatchesCoordinate(
            request.DataClassKey,
            request.ExecutionPolicyVersion))
        {
            return Result.Failure<IngestionRetentionExecutionStart>(
                IngestionRetentionExecutionErrors.CoordinateInvalid);
        }
        else if (execution.State == IngestionRetentionExecutionState.Running &&
            request.Attempt > execution.Attempt)
        {
            Result retried = execution.BeginRetry(
                request.Attempt,
                request.StartedAtUtc,
                request.DeadlineUtc);
            if (retried.IsFailure)
            {
                return Result.Failure<IngestionRetentionExecutionStart>(
                    retried.Error);
            }
        }
        else if (execution.State == IngestionRetentionExecutionState.Running &&
            request.Attempt != execution.Attempt)
        {
            return Result.Failure<IngestionRetentionExecutionStart>(
                IngestionRetentionExecutionErrors.CoordinateInvalid);
        }

        return Result.Success(new IngestionRetentionExecutionStart(
            execution.State == IngestionRetentionExecutionState.Running,
            execution.State == IngestionRetentionExecutionState.Running
                ? null
                : ToResult(execution)));
    }

    private static RetentionContributionResult ToResult(
        IngestionRetentionExecution execution) => new(
            RetentionExecutionContract.CurrentVersion,
            execution.State == IngestionRetentionExecutionState.Blocked
                ? RetentionContributionStatus.Blocked
                : RetentionContributionStatus.Completed,
            execution.AffectedCount,
            execution.AffectedCount,
            execution.RemainingCount!.Value,
            execution.OutcomeCode!,
            execution.CompletedAtUtc!.Value,
            execution.HoldReviewDueAtUtc);
}

internal sealed class CompleteIngestionRetentionExecutionCommandHandler(
    IIngestionRetentionExecutionRepository executions)
    : ICommandHandler<
        CompleteIngestionRetentionExecutionCommand,
        RetentionContributionResult>
{
    public async Task<Result<RetentionContributionResult>> HandleAsync(
        CompleteIngestionRetentionExecutionCommand command,
        CancellationToken cancellationToken)
    {
        IngestionRetentionExecution? execution = await executions.GetAsync(
            command.ExecutionId,
            cancellationToken).ConfigureAwait(false);
        if (execution is null)
        {
            return Result.Failure<RetentionContributionResult>(
                IngestionApplicationErrors.RetentionExecutionNotFound);
        }

        IngestionRetentionExecutionState state =
            command.HoldReviewDueAtUtc is null
                ? IngestionRetentionExecutionState.Completed
                : IngestionRetentionExecutionState.Blocked;
        Result completed = execution.Complete(
            state,
            command.RemainingCount,
            command.OutcomeCode,
            command.CompletedAtUtc,
            command.HoldReviewDueAtUtc);
        return completed.IsFailure
            ? Result.Failure<RetentionContributionResult>(completed.Error)
            : Result.Success(new RetentionContributionResult(
                RetentionExecutionContract.CurrentVersion,
                state == IngestionRetentionExecutionState.Blocked
                    ? RetentionContributionStatus.Blocked
                    : RetentionContributionStatus.Completed,
                execution.AffectedCount,
                execution.AffectedCount,
                execution.RemainingCount!.Value,
                execution.OutcomeCode!,
                execution.CompletedAtUtc!.Value,
                execution.HoldReviewDueAtUtc));
    }
}

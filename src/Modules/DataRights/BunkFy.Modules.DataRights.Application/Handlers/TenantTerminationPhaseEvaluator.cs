namespace BunkFy.Modules.DataRights.Application.Handlers;

using BunkFy.Modules.DataRights.Application.Models;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Models;
using Gma.Framework.Results;

internal sealed class TenantTerminationPhaseEvaluator(
    TenantTerminationPhasePlanner planner)
{
    internal const string BlockedOutcomeCode =
        "tenant-termination.owner-blocked";
    internal const string FailedOutcomeCode =
        "tenant-termination.owner-failed";

    public Result<TenantTerminationPhaseEvaluation> Evaluate(
        TenantTerminationProcess process,
        IReadOnlyCollection<TenantTerminationOwnerWorkItem> workItems)
    {
        Result<TenantTerminationValidatedPhase> validated =
            planner.ValidateWorkItems(process, workItems);
        if (validated.IsFailure)
        {
            return Result.Failure<TenantTerminationPhaseEvaluation>(
                validated.Error);
        }

        TenantTerminationOwnerWorkItem[] exact = validated.Value
            .WorkByOwner.Values
            .ToArray();
        bool hasProcessing = exact.Any(item =>
            item.State == TenantTerminationOwnerWorkState.Processing);
        bool hasFailed = exact.Any(item =>
            item.State == TenantTerminationOwnerWorkState.Failed);
        TenantTerminationOwnerWorkItem[] blocked = exact
            .Where(item =>
                item.State == TenantTerminationOwnerWorkState.Blocked)
            .ToArray();

        if (hasFailed || blocked.Length > 0)
        {
            if (hasProcessing)
            {
                return Running(ReadyDispatches: []);
            }

            return hasFailed
                ? Result.Success(new TenantTerminationPhaseEvaluation(
                    TenantTerminationPhaseDisposition.Failed,
                    FailedOutcomeCode,
                    HoldReviewAtUtc: null,
                    ReadyDispatches: []))
                : Result.Success(new TenantTerminationPhaseEvaluation(
                    TenantTerminationPhaseDisposition.Blocked,
                    BlockedOutcomeCode,
                    FindReviewAtUtc(blocked),
                    ReadyDispatches: []));
        }

        if (exact.All(item =>
                item.State == TenantTerminationOwnerWorkState.Completed))
        {
            return Result.Success(new TenantTerminationPhaseEvaluation(
                TenantTerminationPhaseDisposition.Completed,
                OutcomeCode: null,
                HoldReviewAtUtc: null,
                ReadyDispatches: []));
        }

        Result<IReadOnlyList<TenantTerminationPlannedDispatch>> ready =
            planner.FindReadyDispatches(process, workItems);
        if (ready.IsFailure)
        {
            return Result.Failure<TenantTerminationPhaseEvaluation>(
                ready.Error);
        }

        bool hasAwaitingWork = exact.Any(item => item.State is
            TenantTerminationOwnerWorkState.Prepared or
            TenantTerminationOwnerWorkState.RetryRequired);
        if (!hasProcessing &&
            (!hasAwaitingWork || ready.Value.Count == 0))
        {
            return Invalid();
        }

        return Running(ready.Value);
    }

    private static DateTimeOffset? FindReviewAtUtc(
        IReadOnlyCollection<TenantTerminationOwnerWorkItem> blocked)
    {
        if (blocked.Any(item => !item.HoldReviewAtUtc.HasValue))
        {
            return null;
        }

        return blocked.Min(item => item.HoldReviewAtUtc!.Value);
    }

    private static Result<TenantTerminationPhaseEvaluation> Running(
        IReadOnlyList<TenantTerminationPlannedDispatch> ReadyDispatches) =>
        Result.Success(new TenantTerminationPhaseEvaluation(
            TenantTerminationPhaseDisposition.Running,
            OutcomeCode: null,
            HoldReviewAtUtc: null,
            ReadyDispatches));

    private static Result<TenantTerminationPhaseEvaluation> Invalid() =>
        Result.Failure<TenantTerminationPhaseEvaluation>(
            DataRightsApplicationErrors
                .TenantTerminationExecutionStateInvalid);
}

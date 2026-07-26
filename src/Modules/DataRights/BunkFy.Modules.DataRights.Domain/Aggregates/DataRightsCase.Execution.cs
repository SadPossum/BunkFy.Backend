namespace BunkFy.Modules.DataRights.Domain.Aggregates;

using BunkFy.Modules.DataRights.Domain.Errors;
using BunkFy.Modules.DataRights.Domain.Models;
using Gma.Framework.Results;

public sealed partial class DataRightsCase
{
    public Result BeginAnonymisationExecution(
        long expectedVersion,
        string actorId,
        DateTimeOffset nowUtc)
    {
        Result ready = this.EnsureTransition(
            expectedVersion,
            actorId,
            nowUtc,
            DataRightsCaseState.Approved);
        if (ready.IsFailure)
        {
            return ready;
        }

        if (this.Decision != DataRightsCaseDecision.Approved ||
            this.RequestedOperations != DataRightsCaseOperation.Anonymisation ||
            this.DecisionRevision is null ||
            this.ApprovalPolicyEvidence is null)
        {
            return Result.Failure(DataRightsDomainErrors.AnonymisationApprovalInvalid);
        }

        if (this.selectedSubjects.Count is <= 0 or > MaxSelectedSubjects)
        {
            return Result.Failure(DataRightsDomainErrors.AnonymisationSubjectCountInvalid);
        }

        string normalizedActor = actorId.Trim();
        if (this.ApprovalPolicyEvidence.RequiresDistinctExecutor &&
            string.Equals(normalizedActor, this.DecidedBy, StringComparison.Ordinal))
        {
            return Result.Failure(DataRightsDomainErrors.DecisionActorCannotExecute);
        }

        this.Status = DataRightsCaseState.Executing;
        this.ExecutionStartedBy = normalizedActor;
        this.ExecutionStartedAtUtc = nowUtc;
        this.CompleteChange(normalizedActor, nowUtc);
        this.ExecutionRevision = this.Version;
        return Result.Success();
    }

    public Result ReconcileAnonymisationExecution(
        long expectedVersion,
        int totalCount,
        int completedCount,
        int noOpCount,
        int blockedCount,
        int failedCount,
        string actorId,
        DateTimeOffset nowUtc)
    {
        if (totalCount <= 0 ||
            totalCount != this.selectedSubjects.Count ||
            completedCount < 0 ||
            noOpCount < 0 ||
            blockedCount < 0 ||
            failedCount < 0 ||
            completedCount + noOpCount + blockedCount + failedCount != totalCount)
        {
            return Result.Failure(DataRightsDomainErrors.ExecutionOutcomeInvalid);
        }

        int successfulCount = completedCount + noOpCount;
        DataRightsCaseState target = successfulCount == totalCount
            ? DataRightsCaseState.Completed
            : successfulCount == 0
                ? DataRightsCaseState.Blocked
                : DataRightsCaseState.PartiallyCompleted;
        if (this.Status == target)
        {
            return Result.Success();
        }

        Result ready = this.EnsureTransition(
            expectedVersion,
            actorId,
            nowUtc,
            DataRightsCaseState.Executing);
        if (ready.IsFailure)
        {
            return ready;
        }

        this.Status = target;
        this.CompleteChange(actorId, nowUtc);
        return Result.Success();
    }
}

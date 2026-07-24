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

        if (this.selectedSubjects.Count != 1)
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
}

namespace BunkFy.Modules.DataRights.Domain.Aggregates;

using BunkFy.Modules.DataRights.Domain.Errors;
using BunkFy.Modules.DataRights.Domain.Models;
using Gma.Framework.Results;

public sealed partial class DataRightsCase
{
    public Result BeginCorrectionExecution(
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
            this.DecisionReason != DataRightsCaseDecisionReason.RequestValidated ||
            this.RequestedOperations != DataRightsCaseOperation.Correction ||
            this.DecisionRevision is null ||
            this.selectedSubjects.Count != 1)
        {
            return Result.Failure(DataRightsDomainErrors.CorrectionExecutionInvalid);
        }

        this.Status = DataRightsCaseState.Executing;
        this.ExecutionStartedBy = actorId.Trim();
        this.ExecutionStartedAtUtc = nowUtc;
        this.CompleteChange(actorId, nowUtc);
        this.ExecutionRevision = this.Version;
        return Result.Success();
    }

    public Result CompleteCorrectionExecution(
        long expectedVersion,
        long approvalRevision,
        string actorId,
        DateTimeOffset completedAtUtc,
        DateTimeOffset nowUtc)
    {
        if (this.Status == DataRightsCaseState.Completed &&
            this.RequestedOperations == DataRightsCaseOperation.Correction &&
            this.DecisionRevision == approvalRevision)
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

        if (this.Decision != DataRightsCaseDecision.Approved ||
            this.DecisionReason != DataRightsCaseDecisionReason.RequestValidated ||
            this.RequestedOperations != DataRightsCaseOperation.Correction ||
            this.DecisionRevision != approvalRevision ||
            this.ExecutionStartedAtUtc is null ||
            completedAtUtc < this.ExecutionStartedAtUtc ||
            completedAtUtc > nowUtc)
        {
            return Result.Failure(DataRightsDomainErrors.CorrectionExecutionInvalid);
        }

        this.Status = DataRightsCaseState.Completed;
        this.CompleteChange(actorId, nowUtc);
        return Result.Success();
    }
}

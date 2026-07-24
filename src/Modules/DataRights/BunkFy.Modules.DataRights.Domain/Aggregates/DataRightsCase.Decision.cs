namespace BunkFy.Modules.DataRights.Domain.Aggregates;

using BunkFy.Modules.DataRights.Domain.Errors;
using BunkFy.Modules.DataRights.Domain.Models;
using BunkFy.Modules.DataRights.Domain.ValueObjects;
using Gma.Framework.Results;

public sealed partial class DataRightsCase
{
    public Result BeginDecision(long expectedVersion, string actorId, DateTimeOffset nowUtc)
    {
        Result ready = this.EnsureTransition(
            expectedVersion,
            actorId,
            nowUtc,
            DataRightsCaseState.ReviewRequired);
        if (ready.IsFailure)
        {
            return ready;
        }

        this.Status = DataRightsCaseState.DecisionPending;
        this.CompleteChange(actorId, nowUtc);
        return Result.Success();
    }

    public Result RecordDecision(
        DataRightsCaseDecision decision,
        DataRightsCaseDecisionReason reason,
        long expectedVersion,
        string actorId,
        DateTimeOffset nowUtc,
        DataRightsApprovalPolicyEvidence? approvalPolicyEvidence = null)
    {
        Result ready = this.EnsureTransition(
            expectedVersion,
            actorId,
            nowUtc,
            DataRightsCaseState.DecisionPending);
        if (ready.IsFailure)
        {
            return ready;
        }

        if (!IsValidDecision(decision, reason))
        {
            return Result.Failure(DataRightsDomainErrors.DecisionInvalid);
        }

        bool approvesAnonymisation = decision == DataRightsCaseDecision.Approved &&
            this.RequestedOperations == DataRightsCaseOperation.Anonymisation;
        bool requestsAnonymisation = (this.RequestedOperations & DataRightsCaseOperation.Anonymisation) != 0;
        if ((approvesAnonymisation && approvalPolicyEvidence is null) ||
            (!approvesAnonymisation && approvalPolicyEvidence is not null) ||
            (decision == DataRightsCaseDecision.Approved &&
             requestsAnonymisation &&
             this.RequestedOperations != DataRightsCaseOperation.Anonymisation))
        {
            return Result.Failure(DataRightsDomainErrors.ApprovalPolicyEvidenceInvalid);
        }

        this.Status = decision == DataRightsCaseDecision.Approved
            ? DataRightsCaseState.Approved
            : DataRightsCaseState.Denied;
        this.Decision = decision;
        this.DecisionReason = reason;
        this.DecidedBy = actorId.Trim();
        this.DecidedAtUtc = nowUtc;
        this.ApprovalPolicyEvidence = approvalPolicyEvidence;
        this.CompleteChange(actorId, nowUtc);
        this.DecisionRevision = this.Version;
        return Result.Success();
    }

    private static bool IsValidDecision(
        DataRightsCaseDecision decision,
        DataRightsCaseDecisionReason reason) =>
        (decision == DataRightsCaseDecision.Approved &&
            reason == DataRightsCaseDecisionReason.RequestValidated) ||
        (decision == DataRightsCaseDecision.Denied &&
            reason is >= DataRightsCaseDecisionReason.IdentityOrAuthorityNotEstablished
                and <= DataRightsCaseDecisionReason.UnsupportedOperation);
}

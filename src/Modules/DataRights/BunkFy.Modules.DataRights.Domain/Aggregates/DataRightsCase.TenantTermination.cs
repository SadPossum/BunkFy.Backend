namespace BunkFy.Modules.DataRights.Domain.Aggregates;

using System.Security.Cryptography;
using BunkFy.Modules.DataRights.Domain.Errors;
using BunkFy.Modules.DataRights.Domain.Models;
using Gma.Framework.Results;

public sealed partial class DataRightsCase
{
    public Result PrepareTenantTerminationReview(
        bool exportRequested,
        long expectedVersion,
        string actorId,
        DateTimeOffset nowUtc)
    {
        Result ready = this.EnsureTransition(
            expectedVersion,
            actorId,
            nowUtc,
            DataRightsCaseState.Draft);
        if (ready.IsFailure)
        {
            return ready;
        }

        if (this.Kind != DataRightsCaseKind.TenantTermination ||
            this.RequestedOperations !=
                DataRightsCaseOperation.Anonymisation ||
            this.RequesterRelationship is not
                DataRightsRequesterRelation.ControllerInitiated and not
                DataRightsRequesterRelation.TenantOwner ||
            this.VerificationStatus !=
                DataRightsVerificationState.NotRequired ||
            this.RoutingStatus != DataRightsRoutingState.NotRequired ||
            this.selectedSubjects.Count != 0 ||
            this.TenantTerminationExportRequested.HasValue ||
            this.TenantTerminationPolicyEvidenceSha256 is not null)
        {
            return Result.Failure(
                DataRightsDomainErrors.TenantTerminationTransitionInvalid);
        }

        this.TenantTerminationExportRequested = exportRequested;
        this.Status = DataRightsCaseState.ReviewRequired;
        this.CompleteChange(actorId, nowUtc);
        return Result.Success();
    }

    public Result RecordTenantTerminationDecision(
        DataRightsCaseDecision decision,
        DataRightsCaseDecisionReason reason,
        string? policyEvidenceSha256,
        long expectedVersion,
        string actorId,
        DateTimeOffset nowUtc)
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

        string? normalizedEvidence = policyEvidenceSha256?.Trim();
        bool approved = decision == DataRightsCaseDecision.Approved;
        if (this.Kind != DataRightsCaseKind.TenantTermination ||
            this.RequestedOperations !=
                DataRightsCaseOperation.Anonymisation ||
            !this.TenantTerminationExportRequested.HasValue ||
            this.selectedSubjects.Count != 0 ||
            !IsValidDecision(decision, reason) ||
            (approved &&
                !TenantTerminationProcess.IsSha256(normalizedEvidence)) ||
            (!approved && normalizedEvidence is not null))
        {
            return Result.Failure(
                DataRightsDomainErrors.TenantTerminationTransitionInvalid);
        }

        this.Status = approved
            ? DataRightsCaseState.Approved
            : DataRightsCaseState.Denied;
        this.Decision = decision;
        this.DecisionReason = reason;
        this.DecidedBy = actorId.Trim();
        this.DecidedAtUtc = nowUtc;
        this.TenantTerminationPolicyEvidenceSha256 = normalizedEvidence;
        this.CompleteChange(actorId, nowUtc);
        this.DecisionRevision = this.Version;
        return Result.Success();
    }

    public Result BeginTenantTerminationExecution(
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

        string normalizedActor = actorId.Trim();
        if (this.Kind != DataRightsCaseKind.TenantTermination ||
            this.RequestedOperations !=
                DataRightsCaseOperation.Anonymisation ||
            this.Decision != DataRightsCaseDecision.Approved ||
            this.DecisionRevision is null ||
            this.DecidedAtUtc is null ||
            !this.TenantTerminationExportRequested.HasValue ||
            !TenantTerminationProcess.IsSha256(
                this.TenantTerminationPolicyEvidenceSha256) ||
            string.Equals(
                normalizedActor,
                this.DecidedBy,
                StringComparison.Ordinal))
        {
            return Result.Failure(
                DataRightsDomainErrors.TenantTerminationExecutorInvalid);
        }

        this.Status = DataRightsCaseState.Executing;
        this.ExecutionStartedBy = normalizedActor;
        this.ExecutionStartedAtUtc = nowUtc;
        this.CompleteChange(normalizedActor, nowUtc);
        this.ExecutionRevision = this.Version;
        return Result.Success();
    }

    public Result CompleteTenantTerminationExecution(
        long approvalRevision,
        string policyEvidenceSha256,
        long expectedVersion,
        string actorId,
        DateTimeOffset nowUtc) =>
        this.CompleteTenantTermination(
            DataRightsCaseState.Completed,
            approvalRevision,
            policyEvidenceSha256,
            expectedVersion,
            actorId,
            nowUtc);

    public Result CompleteTenantTerminationCancellation(
        long approvalRevision,
        string policyEvidenceSha256,
        long expectedVersion,
        string actorId,
        DateTimeOffset nowUtc) =>
        this.CompleteTenantTermination(
            DataRightsCaseState.Canceled,
            approvalRevision,
            policyEvidenceSha256,
            expectedVersion,
            actorId,
            nowUtc);

    public bool MatchesTenantTerminationRequest(
        bool exportRequested,
        DataRightsRequesterRelation requesterRelationship) =>
        this.Kind == DataRightsCaseKind.TenantTermination &&
        this.RequestedOperations ==
            DataRightsCaseOperation.Anonymisation &&
        this.RequesterRelationship == requesterRelationship &&
        this.TenantTerminationExportRequested == exportRequested &&
        this.selectedSubjects.Count == 0;

    private Result CompleteTenantTermination(
        DataRightsCaseState terminalState,
        long approvalRevision,
        string policyEvidenceSha256,
        long expectedVersion,
        string actorId,
        DateTimeOffset nowUtc)
    {
        if (this.Status == terminalState &&
            this.DecisionRevision == approvalRevision &&
            FixedTimeSha256Equals(
                this.TenantTerminationPolicyEvidenceSha256,
                policyEvidenceSha256))
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

        if (terminalState is not DataRightsCaseState.Completed and not
                DataRightsCaseState.Canceled ||
            this.Kind != DataRightsCaseKind.TenantTermination ||
            this.DecisionRevision != approvalRevision ||
            !FixedTimeSha256Equals(
                this.TenantTerminationPolicyEvidenceSha256,
                policyEvidenceSha256))
        {
            return Result.Failure(
                DataRightsDomainErrors.TenantTerminationTransitionInvalid);
        }

        this.Status = terminalState;
        this.CompleteChange(actorId, nowUtc);
        return Result.Success();
    }

    private static bool FixedTimeSha256Equals(string? left, string? right)
    {
        if (!TenantTerminationProcess.IsSha256(left) ||
            !TenantTerminationProcess.IsSha256(right))
        {
            return false;
        }

        byte[] leftBytes = Convert.FromHexString(left!);
        byte[] rightBytes = Convert.FromHexString(right!);
        try
        {
            return CryptographicOperations.FixedTimeEquals(
                leftBytes,
                rightBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(leftBytes);
            CryptographicOperations.ZeroMemory(rightBytes);
        }
    }
}

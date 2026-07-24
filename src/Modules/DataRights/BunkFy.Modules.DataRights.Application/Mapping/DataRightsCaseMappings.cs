namespace BunkFy.Modules.DataRights.Application.Mapping;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Domain.Aggregates;

public static class DataRightsCaseMappings
{
    public static DataRightsCaseDto ToDto(this DataRightsCase dataRightsCase) => new(
        dataRightsCase.Id,
        dataRightsCase.PropertyId,
        (DataRightsCaseType)dataRightsCase.Kind,
        (DataRightsOperation)dataRightsCase.RequestedOperations,
        (DataRightsRestrictionDirective)dataRightsCase.RestrictionAction,
        (DataRightsRequesterRelationship)dataRightsCase.RequesterRelationship,
        (DataRightsVerificationStatus)dataRightsCase.VerificationStatus,
        (DataRightsRoutingStatus)dataRightsCase.RoutingStatus,
        (DataRightsCaseStatus)dataRightsCase.Status,
        (DataRightsDecisionOutcome)dataRightsCase.Decision,
        (DataRightsDecisionReason)dataRightsCase.DecisionReason,
        dataRightsCase.DecisionRevision,
        dataRightsCase.DecidedAtUtc,
        dataRightsCase.ExecutionRevision,
        dataRightsCase.ExecutionStartedAtUtc,
        dataRightsCase.SelectedSubjects.Count,
        dataRightsCase.DueAtUtc,
        dataRightsCase.Version,
        dataRightsCase.CreatedAtUtc,
        dataRightsCase.LastChangedAtUtc,
        dataRightsCase.ToApprovalEvidence());

    public static DataRightsApprovalEvidence? ToApprovalEvidence(
        this DataRightsCase dataRightsCase) =>
        dataRightsCase.ApprovalPolicyEvidence is null
            ? null
            : new DataRightsApprovalEvidence(
                dataRightsCase.ApprovalPolicyEvidence.SchemaVersion,
                dataRightsCase.ApprovalPolicyEvidence.PropertyId,
                dataRightsCase.ApprovalPolicyEvidence.PropertyVersion,
                dataRightsCase.ApprovalPolicyEvidence.OperatingCountryCode,
                dataRightsCase.ApprovalPolicyEvidence.PolicyId,
                dataRightsCase.ApprovalPolicyEvidence.PolicyVersion,
                dataRightsCase.ApprovalPolicyEvidence.RetentionPolicyId,
                dataRightsCase.ApprovalPolicyEvidence.RetentionPolicyVersion,
                dataRightsCase.ApprovalPolicyEvidence.ContentSha256,
                dataRightsCase.ApprovalPolicyEvidence.PurposeCode,
                dataRightsCase.ApprovalPolicyEvidence.Surface,
                dataRightsCase.ApprovalPolicyEvidence.SourceProvenance,
                dataRightsCase.ApprovalPolicyEvidence.EvaluatedAtUtc,
                dataRightsCase.ApprovalPolicyEvidence.RequiresDistinctExecutor);
}

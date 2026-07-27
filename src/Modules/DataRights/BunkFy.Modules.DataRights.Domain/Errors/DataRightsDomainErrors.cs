namespace BunkFy.Modules.DataRights.Domain.Errors;

using Gma.Framework.Results;

public static class DataRightsDomainErrors
{
    public static readonly Error CaseIdRequired = new(
        "DataRights.CaseIdRequired",
        "A data-rights case id is required.");
    public static readonly Error TenantInvalid = new(
        "DataRights.TenantInvalid",
        "The tenant id is invalid.");
    public static readonly Error PropertyRequired = new(
        "DataRights.PropertyRequired",
        "A guest data-rights case requires a property.");
    public static readonly Error PropertyNotAllowed = new(
        "DataRights.PropertyNotAllowed",
        "A tenant-scoped data-rights case cannot be limited to one property.");
    public static readonly Error CaseTypeInvalid = new(
        "DataRights.CaseTypeInvalid",
        "The data-rights case type is invalid.");
    public static readonly Error OperationsInvalid = new(
        "DataRights.OperationsInvalid",
        "The requested data-rights operations are invalid.");
    public static readonly Error RestrictionDirectiveInvalid = new(
        "DataRights.RestrictionDirectiveInvalid",
        "A restriction request must declare exactly one supported directive.");
    public static readonly Error RequesterRelationshipInvalid = new(
        "DataRights.RequesterRelationshipInvalid",
        "The requester relationship is invalid.");
    public static readonly Error GuestRightsRequesterInvalid = new(
        "DataRights.GuestRightsRequesterInvalid",
        "A guest data-rights case must be initiated by the data subject, an authorized representative, or the controller.");
    public static readonly Error StaffRightsRequesterInvalid = new(
        "DataRights.StaffRightsRequesterInvalid",
        "A staff data-rights case must be initiated by the data subject, an authorized representative, or the controller.");
    public static readonly Error StaffRightsOperationsInvalid = new(
        "DataRights.StaffRightsOperationsInvalid",
        "A staff data-rights case currently supports access export only.");
    public static readonly Error TenantTerminationRequesterInvalid = new(
        "DataRights.TenantTerminationRequesterInvalid",
        "Tenant termination must be initiated by the controller or tenant owner.");
    public static readonly Error ActorInvalid = new(
        "DataRights.ActorInvalid",
        "The actor id is invalid.");
    public static readonly Error TimestampInvalid = new(
        "DataRights.TimestampInvalid",
        "The case timestamp is invalid.");
    public static readonly Error VersionConflict = new(
        "DataRights.VersionConflict",
        "The data-rights case version has changed.");
    public static readonly Error TransitionInvalid = new(
        "DataRights.TransitionInvalid",
        "The data-rights case cannot make that transition.");
    public static readonly Error VerificationRequired = new(
        "DataRights.VerificationRequired",
        "Requester verification must succeed before sensitive discovery.");
    public static readonly Error ControllerRoutingRequired = new(
        "DataRights.ControllerRoutingRequired",
        "Controller routing must complete before sensitive discovery.");
    public static readonly Error SubjectCoordinateInvalid = new(
        "DataRights.SubjectCoordinateInvalid",
        "The selected subject coordinate is invalid.");
    public static readonly Error SubjectAlreadySelected = new(
        "DataRights.SubjectAlreadySelected",
        "The subject coordinate is already selected.");
    public static readonly Error SubjectNotSelected = new(
        "DataRights.SubjectNotSelected",
        "The subject coordinate is not selected.");
    public static readonly Error SubjectSelectionLimitReached = new(
        "DataRights.SubjectSelectionLimitReached",
        "The case has reached its selected-subject limit.");
    public static readonly Error SubjectSelectionRequired = new(
        "DataRights.SubjectSelectionRequired",
        "Select at least one subject coordinate before review.");
    public static readonly Error DecisionInvalid = new(
        "DataRights.DecisionInvalid",
        "The data-rights decision and reason combination is invalid.");
    public static readonly Error ApprovalPolicyEvidenceInvalid = new(
        "DataRights.ApprovalPolicyEvidenceInvalid",
        "An anonymisation approval requires valid immutable policy evidence.");
    public static readonly Error AnonymisationApprovalInvalid = new(
        "DataRights.AnonymisationApprovalInvalid",
        "The case does not contain an executable anonymisation approval.");
    public static readonly Error AnonymisationSubjectCountInvalid = new(
        "DataRights.AnonymisationSubjectCountInvalid",
        "An anonymisation execution requires at least one selected subject.");
    public static readonly Error DecisionActorCannotExecute = new(
        "DataRights.DecisionActorCannotExecute",
        "The decision actor cannot execute this anonymisation.");
    public static readonly Error ExecutionCoordinateInvalid = new(
        "DataRights.ExecutionCoordinateInvalid",
        "The data-rights execution coordinate is invalid.");
    public static readonly Error ExecutionTaskConflict = new(
        "DataRights.ExecutionTaskConflict",
        "The data-rights execution is already bound to another task or attempt.");
    public static readonly Error ExecutionOwnerResultInvalid = new(
        "DataRights.ExecutionOwnerResultInvalid",
        "The data-rights owner result is invalid or conflicts with durable proof.");
    public static readonly Error ExecutionOutcomeInvalid = new(
        "DataRights.ExecutionOutcomeInvalid",
        "The data-rights execution outcome does not match its selected subjects.");
    public static readonly Error RecordPseudonymInvalid = new(
        "DataRights.RecordPseudonymInvalid",
        "The data-rights record pseudonym is invalid.");
    public static readonly Error RecordPseudonymKeyUnavailable = new(
        "DataRights.RecordPseudonymKeyUnavailable",
        "The requested data-rights record pseudonym key is unavailable.");
    public static readonly Error ProcessingLedgerEntryInvalid = new(
        "DataRights.ProcessingLedgerEntryInvalid",
        "The data-rights processing ledger entry is invalid.");
    public static readonly Error ReplayEnvelopeInvalid = new(
        "DataRights.ReplayEnvelopeInvalid",
        "The data-rights replay envelope is invalid.");
    public static readonly Error ReplayEnvelopeKeyUnavailable = new(
        "DataRights.ReplayEnvelopeKeyUnavailable",
        "The requested data-rights replay-envelope key is unavailable.");
    public static readonly Error RestoreCheckpointInvalid = new(
        "DataRights.RestoreCheckpointInvalid",
        "The data-rights restore checkpoint is invalid.");
    public static readonly Error RestoreCheckpointConflict = new(
        "DataRights.RestoreCheckpointConflict",
        "The data-rights restore checkpoint changed during reconciliation.");
    public static readonly Error ExportArtifactCoordinateInvalid = new(
        "DataRights.ExportArtifactCoordinateInvalid",
        "The protected export artifact coordinate is invalid.");
    public static readonly Error ExportArtifactGenerationInvalid = new(
        "DataRights.ExportArtifactGenerationInvalid",
        "The protected export generation attempt is invalid.");
    public static readonly Error ExportArtifactTransitionInvalid = new(
        "DataRights.ExportArtifactTransitionInvalid",
        "The protected export artifact cannot make that transition.");
    public static readonly Error ExportArtifactCompletionInvalid = new(
        "DataRights.ExportArtifactCompletionInvalid",
        "The protected export artifact completion proof is invalid.");
    public static readonly Error ExportArtifactFailureInvalid = new(
        "DataRights.ExportArtifactFailureInvalid",
        "The protected export artifact failure proof is invalid.");
    public static readonly Error ExportArtifactDeletionInvalid = new(
        "DataRights.ExportArtifactDeletionInvalid",
        "The protected export artifact deletion proof is invalid.");
    public static readonly Error ExportAuditEntryInvalid = new(
        "DataRights.ExportAuditEntryInvalid",
        "The protected export audit fact is invalid.");
    public static readonly Error AccessExportCompletionInvalid = new(
        "DataRights.AccessExportCompletionInvalid",
        "The case does not contain the matching approved access export.");
}

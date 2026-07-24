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
        "A tenant-termination case cannot be limited to one property.");
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
}

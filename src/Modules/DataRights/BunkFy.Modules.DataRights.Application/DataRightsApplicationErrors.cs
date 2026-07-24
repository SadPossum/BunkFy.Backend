namespace BunkFy.Modules.DataRights.Application;

using BunkFy.Modules.DataRights.Domain.Errors;
using Gma.Framework.Results;

public static class DataRightsApplicationErrors
{
    public static readonly Error TenantRequired = new(
        "DataRights.TenantRequired",
        "A tenant scope is required.");
    public static readonly Error CaseNotFound = new(
        "DataRights.CaseNotFound",
        "The data-rights case was not found.");
    public static readonly Error DiscoveryCriteriaInvalid = new(
        "DataRights.DiscoveryCriteriaInvalid",
        "Sensitive discovery requires exactly one valid guest id, email, or phone.");
    public static readonly Error DiscoveryScopeUnavailable = new(
        "DataRights.DiscoveryScopeUnavailable",
        "The requested property is not available in the owner projection.");
    public static readonly Error SubjectOwnerUnavailable = new(
        "DataRights.SubjectOwnerUnavailable",
        "The selected subject owner is unavailable.");
    public static readonly Error SubjectNotFound = new(
        "DataRights.SubjectNotFound",
        "The selected subject is not available in the requested scope.");
    public static readonly Error SubjectStale = new(
        "DataRights.SubjectStale",
        "The selected subject changed and must be rediscovered.");
    public static readonly Error AnonymisationApprovalPolicyDenied = new(
        "DataRights.AnonymisationApprovalPolicyDenied",
        "The property policy does not authorize anonymisation.");
    public static readonly Error AnonymisationMustBeApprovedSeparately = new(
        "DataRights.AnonymisationMustBeApprovedSeparately",
        "Anonymisation must be reviewed and approved as the case's only operation.");
    public static readonly Error AnonymisationExecutionDenied = new(
        "DataRights.AnonymisationExecutionDenied",
        "The approved anonymisation is not executable with the current evidence.");
    public static readonly Error ExecutionNotFound = new(
        "DataRights.ExecutionNotFound",
        "The data-rights execution was not found.");
    public static readonly Error ExecutionAlreadyStarted = new(
        "DataRights.ExecutionAlreadyStarted",
        "The data-rights execution was already started with different coordinates.");

    public static Error VersionConflict => DataRightsDomainErrors.VersionConflict;
    public static Error TransitionInvalid => DataRightsDomainErrors.TransitionInvalid;
    public static Error VerificationRequired => DataRightsDomainErrors.VerificationRequired;
    public static Error ControllerRoutingRequired => DataRightsDomainErrors.ControllerRoutingRequired;
    public static Error SubjectCoordinateInvalid => DataRightsDomainErrors.SubjectCoordinateInvalid;
    public static Error SubjectAlreadySelected => DataRightsDomainErrors.SubjectAlreadySelected;
    public static Error SubjectNotSelected => DataRightsDomainErrors.SubjectNotSelected;
    public static Error SubjectSelectionLimitReached => DataRightsDomainErrors.SubjectSelectionLimitReached;
    public static Error SubjectSelectionRequired => DataRightsDomainErrors.SubjectSelectionRequired;
    public static Error DecisionInvalid => DataRightsDomainErrors.DecisionInvalid;
    public static Error AnonymisationApprovalInvalid =>
        DataRightsDomainErrors.AnonymisationApprovalInvalid;
    public static Error AnonymisationSubjectCountInvalid =>
        DataRightsDomainErrors.AnonymisationSubjectCountInvalid;
    public static Error DecisionActorCannotExecute =>
        DataRightsDomainErrors.DecisionActorCannotExecute;
    public static Error ExecutionCoordinateInvalid =>
        DataRightsDomainErrors.ExecutionCoordinateInvalid;
}

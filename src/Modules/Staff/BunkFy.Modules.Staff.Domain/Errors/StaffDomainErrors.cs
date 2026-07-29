namespace BunkFy.Modules.Staff.Domain.Errors;

using Gma.Framework.Results;

public static class StaffDomainErrors
{
    public static readonly Error StaffMemberIdRequired = new("Staff.StaffMemberIdRequired", "A staff member id is required.");
    public static readonly Error TenantInvalid = new("Staff.TenantInvalid", "The tenant id is invalid.");
    public static readonly Error DisplayNameInvalid = new("Staff.DisplayNameInvalid", "The display name is invalid.");
    public static readonly Error LegalNameInvalid = new("Staff.LegalNameInvalid", "The legal name is invalid.");
    public static readonly Error EmailInvalid = new("Staff.EmailInvalid", "The work email is invalid.");
    public static readonly Error PhoneInvalid = new("Staff.PhoneInvalid", "The work phone is invalid.");
    public static readonly Error EmployeeNumberInvalid = new("Staff.EmployeeNumberInvalid", "The employee number is invalid.");
    public static readonly Error JobTitleInvalid = new("Staff.JobTitleInvalid", "The job title is invalid.");
    public static readonly Error DepartmentInvalid = new("Staff.DepartmentInvalid", "The department is invalid.");
    public static readonly Error AuthSubjectInvalid = new("Staff.AuthSubjectInvalid", "The Auth subject id is invalid.");
    public static readonly Error ActorInvalid = new("Staff.ActorInvalid", "The actor id is invalid.");
    public static readonly Error ReasonInvalid = new("Staff.ReasonInvalid", "A valid reason is required.");
    public static readonly Error EventIdRequired = new("Staff.EventIdRequired", "A domain event id is required.");
    public static readonly Error AssignmentIdRequired = new("Staff.AssignmentIdRequired", "An assignment id is required.");
    public static readonly Error PropertyIdRequired = new("Staff.PropertyIdRequired", "A property id is required.");
    public static readonly Error AssignmentDateInvalid = new("Staff.AssignmentDateInvalid", "The assignment date range is invalid.");
    public static readonly Error VersionConflict = new("Staff.VersionConflict", "The staff profile version has changed.");
    public static readonly Error StaffSuspended = new("Staff.StaffSuspended", "The staff member is suspended.");
    public static readonly Error StaffDeparted = new("Staff.StaffDeparted", "The staff member has departed.");
    public static readonly Error StaffAnonymised = new(
        "Staff.StaffAnonymised",
        "The staff member has been anonymised.");
    public static readonly Error AlreadySuspended = new("Staff.AlreadySuspended", "The staff member is already suspended.");
    public static readonly Error NotSuspended = new("Staff.NotSuspended", "The staff member is not suspended.");
    public static readonly Error AlreadyDeparted = new("Staff.AlreadyDeparted", "The staff member has already departed.");
    public static readonly Error AssignmentNotFound = new("Staff.AssignmentNotFound", "The current property assignment was not found.");
    public static readonly Error AssignmentAlreadyExists = new("Staff.AssignmentAlreadyExists", "A current assignment for this property already exists.");
    public static readonly Error PrimaryAssignmentExists = new("Staff.PrimaryAssignmentExists", "A current primary property assignment already exists.");
    public static readonly Error CorrectionNoChanges = new(
        "Staff.CorrectionNoChanges",
        "The correction does not change the staff profile.");
    public static readonly Error CorrectionReceiptIdentityInvalid = new(
        "Staff.CorrectionReceiptIdentityInvalid",
        "The correction receipt identity is invalid.");
    public static readonly Error CorrectionReceiptVersionInvalid = new(
        "Staff.CorrectionReceiptVersionInvalid",
        "The correction receipt versions are invalid.");
    public static readonly Error CorrectionReceiptFieldsInvalid = new(
        "Staff.CorrectionReceiptFieldsInvalid",
        "The correction receipt fields are invalid.");
    public static readonly Error RestrictionProjectionIdentityInvalid = new(
        "Staff.RestrictionProjectionIdentityInvalid",
        "The processing-restriction projection identity is invalid.");
    public static readonly Error RestrictionProjectionContractUnsupported = new(
        "Staff.RestrictionProjectionContractUnsupported",
        "The processing-restriction projection contract is unsupported.");
    public static readonly Error RestrictionProjectionVersionConflict = new(
        "Staff.RestrictionProjectionVersionConflict",
        "The processing-restriction projection version has changed.");
    public static readonly Error RestrictionProjectionTransitionInvalid = new(
        "Staff.RestrictionProjectionTransitionInvalid",
        "The processing-restriction transition is invalid.");
    public static readonly Error RestrictionProjectionStateInvalid = new(
        "Staff.RestrictionProjectionStateInvalid",
        "The processing-restriction projection state is invalid.");
    public static readonly Error RestrictionIdentityInvalid = new(
        "Staff.RestrictionIdentityInvalid",
        "The processing-restriction identity is invalid.");
    public static readonly Error RestrictionApprovalInvalid = new(
        "Staff.RestrictionApprovalInvalid",
        "The processing-restriction approval coordinate is invalid.");
    public static readonly Error RestrictionTransitionInvalid = new(
        "Staff.RestrictionTransitionInvalid",
        "The processing-restriction transition is invalid.");
    public static readonly Error RestrictionVersionConflict = new(
        "Staff.RestrictionVersionConflict",
        "The processing-restriction version has changed.");
    public static readonly Error RestrictionAlreadyReleased = new(
        "Staff.RestrictionAlreadyReleased",
        "The processing restriction is already released.");
    public static readonly Error RestrictionReceiptIdentityInvalid = new(
        "Staff.RestrictionReceiptIdentityInvalid",
        "The processing-restriction receipt identity is invalid.");
    public static readonly Error RestrictionReceiptVersionInvalid = new(
        "Staff.RestrictionReceiptVersionInvalid",
        "The processing-restriction receipt version is invalid.");
    public static readonly Error RestrictionReceiptTransitionInvalid = new(
        "Staff.RestrictionReceiptTransitionInvalid",
        "The processing-restriction receipt transition is invalid.");
    public static readonly Error EmploymentGovernanceIdentityInvalid = new(
        "Staff.EmploymentGovernanceIdentityInvalid",
        "The employment-governance identity is invalid.");
    public static readonly Error EmploymentGovernanceBindingInvalid = new(
        "Staff.EmploymentGovernanceBindingInvalid",
        "The employment-governance policy binding is invalid.");
    public static readonly Error EmploymentGovernanceAcknowledgementsInvalid = new(
        "Staff.EmploymentGovernanceAcknowledgementsInvalid",
        "The employment-governance acknowledgements are invalid.");
    public static readonly Error EmploymentGovernanceLifecycleInvalid = new(
        "Staff.EmploymentGovernanceLifecycleInvalid",
        "The employment-governance lifecycle is invalid.");
    public static readonly Error EmploymentGovernanceVersionConflict = new(
        "Staff.EmploymentGovernanceVersionConflict",
        "The employment-governance version has changed.");
    public static readonly Error EmploymentGovernanceReceiptInvalid = new(
        "Staff.EmploymentGovernanceReceiptInvalid",
        "The employment-governance receipt is invalid.");
    public static readonly Error DataHoldIdentityInvalid = new(
        "Staff.DataHoldIdentityInvalid",
        "The Staff data-hold identity is invalid.");
    public static readonly Error DataHoldReasonCodeInvalid = new(
        "Staff.DataHoldReasonCodeInvalid",
        "The Staff data-hold reason code is invalid.");
    public static readonly Error DataHoldLifecycleInvalid = new(
        "Staff.DataHoldLifecycleInvalid",
        "The Staff data-hold lifecycle is invalid.");
    public static readonly Error DataHoldVersionConflict = new(
        "Staff.DataHoldVersionConflict",
        "The Staff data-hold version has changed.");
    public static readonly Error DataHoldAlreadyReleased = new(
        "Staff.DataHoldAlreadyReleased",
        "The Staff data hold is already released.");
    public static readonly Error DataHoldReceiptIdentityInvalid = new(
        "Staff.DataHoldReceiptIdentityInvalid",
        "The Staff data-hold receipt identity is invalid.");
    public static readonly Error DataHoldReceiptTransitionInvalid = new(
        "Staff.DataHoldReceiptTransitionInvalid",
        "The Staff data-hold receipt transition is invalid.");
    public static readonly Error AnonymisationTimestampInvalid = new(
        "Staff.AnonymisationTimestampInvalid",
        "The Staff anonymisation timestamp is invalid.");
    public static readonly Error AnonymisationTransitionInvalid = new(
        "Staff.AnonymisationTransitionInvalid",
        "The Staff member is not eligible for anonymisation.");
    public static readonly Error AnonymisationReceiptInvalid = new(
        "Staff.AnonymisationReceiptInvalid",
        "The Staff anonymisation receipt is invalid.");
    public static readonly Error AnonymisationTombstoneInvalid = new(
        "Staff.AnonymisationTombstoneInvalid",
        "The Staff anonymisation tombstone is invalid.");
}

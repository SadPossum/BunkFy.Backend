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
    public static readonly Error AlreadySuspended = new("Staff.AlreadySuspended", "The staff member is already suspended.");
    public static readonly Error NotSuspended = new("Staff.NotSuspended", "The staff member is not suspended.");
    public static readonly Error AlreadyDeparted = new("Staff.AlreadyDeparted", "The staff member has already departed.");
    public static readonly Error AssignmentNotFound = new("Staff.AssignmentNotFound", "The current property assignment was not found.");
    public static readonly Error AssignmentAlreadyExists = new("Staff.AssignmentAlreadyExists", "A current assignment for this property already exists.");
    public static readonly Error PrimaryAssignmentExists = new("Staff.PrimaryAssignmentExists", "A current primary property assignment already exists.");
}

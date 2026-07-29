namespace BunkFy.Modules.Staff.Application;

using Gma.Framework.Results;
using BunkFy.Modules.Staff.Domain.Errors;

public static class StaffApplicationErrors
{
    public static readonly Error StaffMemberNotFound = new("Staff.StaffMemberNotFound", "The staff member was not found.");
    public static readonly Error TenantRequired = new("Staff.TenantRequired", "A tenant context is required.");
    public static readonly Error PropertyUnavailable = new("Staff.PropertyUnavailable", "The property is unavailable for staff assignment.");
    public static readonly Error EmployeeNumberConflict = new("Staff.EmployeeNumberConflict", "The employee number is already in use.");
    public static readonly Error AuthSubjectConflict = new("Staff.AuthSubjectConflict", "The Auth subject is already linked to another staff member.");
    public static readonly Error LifecycleTransitionDenied = new("Staff.LifecycleTransitionDenied", "The staff lifecycle change is not allowed by the workspace policy.");
    public static readonly Error LifecycleCoordinationPending = new("Staff.LifecycleCoordinationPending", "Workspace access could not be coordinated yet. Retry the staff lifecycle change.");
    public static readonly Error WorkspaceOwnerProtected = new("Staff.WorkspaceOwnerProtected", "Transfer workspace ownership before changing this staff member's lifecycle.");
    public static readonly Error DataRightsApprovalRequired = new(
        "Staff.DataRightsApprovalRequired",
        "An active approved data-rights correction execution is required.");
    public static readonly Error CorrectionIdempotencyConflict = new(
        "Staff.CorrectionIdempotencyConflict",
        "The correction execution was already used for different coordinates.");
    public static readonly Error CorrectionRequestInvalid = new(
        "Staff.CorrectionRequestInvalid",
        "The correction request is invalid.");
    public static Error VersionConflict => StaffDomainErrors.VersionConflict;
    public static Error StaffSuspended => StaffDomainErrors.StaffSuspended;
    public static Error StaffDeparted => StaffDomainErrors.StaffDeparted;
    public static Error CorrectionNoChanges => StaffDomainErrors.CorrectionNoChanges;
}

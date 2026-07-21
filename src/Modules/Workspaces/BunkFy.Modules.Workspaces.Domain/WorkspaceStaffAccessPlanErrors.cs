namespace BunkFy.Modules.Workspaces.Domain;

using Gma.Framework.Results;

public static class WorkspaceStaffAccessPlanErrors
{
    public static readonly Error Invalid = new(
        "Workspaces.StaffAccessPlanInvalid",
        "The workspace Staff access plan is invalid.");
    public static readonly Error Conflict = new(
        "Workspaces.StaffAccessPlanConflict",
        "The join source id is already bound to another workspace Staff access plan.");
    public static readonly Error StateConflict = new(
        "Workspaces.StaffAccessPlanStateConflict",
        "The workspace Staff access plan cannot make this transition.");
}

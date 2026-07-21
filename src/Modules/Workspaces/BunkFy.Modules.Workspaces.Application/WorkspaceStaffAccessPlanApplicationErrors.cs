namespace BunkFy.Modules.Workspaces.Application;

using Gma.Framework.Results;

public static class WorkspaceStaffAccessPlanApplicationErrors
{
    public static readonly Error PlanNotFound = new(
        "Workspaces.StaffAccessPlanNotFound",
        "The workspace Staff access plan was not found.");
    public static readonly Error ProfileUnavailable = new(
        "Workspaces.StaffAccessProfileUnavailable",
        "The selected workspace access profile is unavailable.");
    public static readonly Error ProfileNotDelegable = new(
        "Workspaces.StaffAccessProfileNotDelegable",
        "The selected workspace access profile cannot be delegated.");
    public static readonly Error PropertyUnavailable = new(
        "Workspaces.StaffAccessPropertyUnavailable",
        "One or more selected properties are unavailable in this workspace.");
    public static readonly Error DelegationDenied = new(
        "Workspaces.StaffAccessDelegationDenied",
        "The actor cannot delegate the selected workspace access plan.");
    public static readonly Error JoinSourceIssuanceFailed = new(
        "Workspaces.JoinSourceIssuanceFailed",
        "The workspace join source could not be issued.");
}

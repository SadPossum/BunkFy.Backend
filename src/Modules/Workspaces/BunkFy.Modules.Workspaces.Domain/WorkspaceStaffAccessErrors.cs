namespace BunkFy.Modules.Workspaces.Domain;

using Gma.Framework.Results;

public static class WorkspaceStaffAccessErrors
{
    public static readonly Error Invalid = new(
        "Workspaces.StaffAccessInvalid",
        "The workspace staff access process is invalid.");
    public static readonly Error StateConflict = new(
        "Workspaces.StaffAccessStateConflict",
        "The workspace staff access process cannot make that transition.");
}

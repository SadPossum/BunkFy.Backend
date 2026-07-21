namespace BunkFy.Modules.Workspaces.Application;

using Gma.Framework.Results;

public static class WorkspaceStaffAccessApplicationErrors
{
    public static readonly Error ProcessNotFound = new(
        "Workspaces.StaffAccessProcessNotFound",
        "The workspace staff access process was not found.");
    public static readonly Error ProcessConflict = new(
        "Workspaces.StaffAccessProcessConflict",
        "Another workspace staff access transition must finish first.");
    public static readonly Error ResumeSnapshotUnavailable = new(
        "Workspaces.StaffAccessResumeSnapshotUnavailable",
        "The previous workspace access snapshot is unavailable.");
    public static readonly Error RetryPending = new(
        "Workspaces.StaffAccessRetryPending",
        "The workspace staff access process still requires its matching Staff transition or another retry.");
    public static readonly Error OwnerProtected = new(
        "Workspaces.StaffAccessOwnerProtected",
        "Transfer workspace ownership before retrying this staff access process.");
}

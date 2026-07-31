namespace BunkFy.Modules.Workspaces.Contracts;

public sealed record WorkspaceTerminationAccessRequirement(
    WorkspaceTerminationAccessPurpose Purpose,
    string ProcessRouteValueName = "processId");

public enum WorkspaceTerminationAccessPurpose
{
    Unknown = 0,
    Review = 1,
    Export = 2,
    Cancellation = 3,
    Recovery = 4
}

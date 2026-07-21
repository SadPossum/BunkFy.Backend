namespace BunkFy.Modules.Workspaces.Domain;

public enum WorkspaceStaffAccessProcessState
{
    Unknown = 0,
    Prepared = 1,
    AwaitingStaffCommit = 2,
    RestorationPending = 3,
    Completed = 4
}

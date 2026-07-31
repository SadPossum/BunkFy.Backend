namespace BunkFy.Modules.Workspaces.Domain.Termination;

public enum WorkspaceTerminationFenceAction
{
    Unknown = 0,
    Freeze = 1,
    BeginDestruction = 2,
    Close = 3,
    Release = 4
}

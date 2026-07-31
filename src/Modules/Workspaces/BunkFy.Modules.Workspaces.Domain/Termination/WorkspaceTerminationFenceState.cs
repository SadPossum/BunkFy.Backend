namespace BunkFy.Modules.Workspaces.Domain.Termination;

public enum WorkspaceTerminationFenceState
{
    Unknown = 0,
    Frozen = 1,
    DestructionStarted = 2,
    Closed = 3,
    Released = 4
}

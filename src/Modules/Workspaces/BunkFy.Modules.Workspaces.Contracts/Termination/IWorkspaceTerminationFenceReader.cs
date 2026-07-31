namespace BunkFy.Modules.Workspaces.Contracts;

public interface IWorkspaceTerminationFenceReader
{
    Task<WorkspaceTerminationFenceSnapshot?> GetCurrentAsync(
        CancellationToken cancellationToken = default);
}

public sealed record WorkspaceTerminationFenceSnapshot(
    Guid ProcessId,
    Guid TerminationEpoch,
    WorkspaceTerminationFenceState State,
    long Version);

public enum WorkspaceTerminationFenceState
{
    Unknown = 0,
    Frozen = 1,
    DestructionStarted = 2,
    Closed = 3
}

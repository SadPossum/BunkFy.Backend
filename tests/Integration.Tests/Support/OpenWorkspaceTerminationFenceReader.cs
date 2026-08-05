namespace Integration.Tests.Support;

using BunkFy.Modules.Workspaces.Contracts;

internal sealed class OpenWorkspaceTerminationFenceReader
    : IWorkspaceTerminationFenceReader
{
    public static OpenWorkspaceTerminationFenceReader Instance { get; } = new();

    private OpenWorkspaceTerminationFenceReader()
    {
    }

    public Task<WorkspaceTerminationFenceSnapshot?> GetCurrentAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<WorkspaceTerminationFenceSnapshot?>(null);
    }
}

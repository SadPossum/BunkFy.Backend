namespace BunkFy.Modules.Workspaces.Tests;

using BunkFy.Modules.Workspaces.Application.Ports;

internal sealed class RecordingWorkspaceCrossGraphMutationLock(
    List<string>? calls = null)
    : IWorkspaceCrossGraphMutationLock
{
    public int AcquireCount { get; private set; }

    public Task AcquireAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        this.AcquireCount++;
        calls?.Add("tenant-exclusive");
        return Task.CompletedTask;
    }
}

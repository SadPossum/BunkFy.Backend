namespace BunkFy.Modules.Workspaces.Application.Ports;

public interface IWorkspaceCrossGraphMutationLock
{
    Task AcquireAsync(CancellationToken cancellationToken);
}

namespace BunkFy.Modules.Workspaces.Application.Ports;

public interface IWorkspaceStaffCorrelationOperationLock
{
    Task<bool> TryAcquireAsync(
        Guid anchorProcessId,
        CancellationToken cancellationToken);
}

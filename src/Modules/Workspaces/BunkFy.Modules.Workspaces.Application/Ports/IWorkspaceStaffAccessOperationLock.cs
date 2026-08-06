namespace BunkFy.Modules.Workspaces.Application.Ports;

public interface IWorkspaceStaffAccessOperationLock
{
    Task AcquireStaffAsync(
        Guid staffMemberId,
        CancellationToken cancellationToken);

    Task<bool> TryAcquireProcessAsync(
        Guid processId,
        CancellationToken cancellationToken);
}

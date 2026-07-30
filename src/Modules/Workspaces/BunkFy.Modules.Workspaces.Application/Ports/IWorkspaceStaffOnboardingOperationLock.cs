namespace BunkFy.Modules.Workspaces.Application.Ports;

public interface IWorkspaceStaffOnboardingOperationLock
{
    Task<bool> TryAcquireAsync(
        Guid applicationId,
        CancellationToken cancellationToken);
}

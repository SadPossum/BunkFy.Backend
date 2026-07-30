namespace BunkFy.Modules.Workspaces.Application.Ports;

public interface IWorkspaceStaffOnboardingDataRightsCorrectionLock
{
    Task<bool> TryAcquireAsync(
        Guid applicationId,
        CancellationToken cancellationToken);
}

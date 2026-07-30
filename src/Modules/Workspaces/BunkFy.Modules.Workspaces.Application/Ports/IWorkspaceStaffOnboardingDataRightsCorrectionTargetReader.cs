namespace BunkFy.Modules.Workspaces.Application.Ports;

using BunkFy.Modules.Workspaces.Application.Models;

public interface
    IWorkspaceStaffOnboardingDataRightsCorrectionTargetReader
{
    Task<WorkspaceStaffOnboardingDataRightsCorrectionTarget?> GetAsync(
        Guid applicationId,
        CancellationToken cancellationToken);
}

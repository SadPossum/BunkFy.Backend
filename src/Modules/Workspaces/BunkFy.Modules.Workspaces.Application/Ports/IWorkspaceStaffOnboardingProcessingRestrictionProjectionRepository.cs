namespace BunkFy.Modules.Workspaces.Application.Ports;

using BunkFy.Modules.Workspaces.Domain.DataRights;

public interface
    IWorkspaceStaffOnboardingProcessingRestrictionProjectionRepository
{
    Task<WorkspaceStaffOnboardingProcessingRestrictionProjection?> GetAsync(
        Guid applicationId,
        CancellationToken cancellationToken);

    Task AddAsync(
        WorkspaceStaffOnboardingProcessingRestrictionProjection projection,
        CancellationToken cancellationToken);
}

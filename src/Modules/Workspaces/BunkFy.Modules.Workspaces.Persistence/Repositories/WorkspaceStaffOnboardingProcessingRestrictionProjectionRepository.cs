namespace BunkFy.Modules.Workspaces.Persistence.Repositories;

using BunkFy.Modules.Workspaces.Application.Ports;
using BunkFy.Modules.Workspaces.Domain.DataRights;
using Microsoft.EntityFrameworkCore;

internal sealed class
    WorkspaceStaffOnboardingProcessingRestrictionProjectionRepository(
        WorkspacesDbContext dbContext)
    : IWorkspaceStaffOnboardingProcessingRestrictionProjectionRepository
{
    public Task<WorkspaceStaffOnboardingProcessingRestrictionProjection?>
        GetAsync(
            Guid applicationId,
            CancellationToken cancellationToken) =>
        dbContext.StaffOnboardingProcessingRestrictionProjections
            .FirstOrDefaultAsync(
                projection => projection.ApplicationId == applicationId,
                cancellationToken);

    public Task AddAsync(
        WorkspaceStaffOnboardingProcessingRestrictionProjection projection,
        CancellationToken cancellationToken)
    {
        dbContext.StaffOnboardingProcessingRestrictionProjections.Add(
            projection);
        return Task.CompletedTask;
    }
}

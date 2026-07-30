namespace BunkFy.Modules.Workspaces.Persistence.Repositories;

using BunkFy.Modules.Workspaces.Application.Models;
using BunkFy.Modules.Workspaces.Application.Ports;
using Microsoft.EntityFrameworkCore;

internal sealed class
    WorkspaceStaffOnboardingDataRightsCorrectionTargetReader(
        WorkspacesDbContext dbContext)
    : IWorkspaceStaffOnboardingDataRightsCorrectionTargetReader
{
    public Task<WorkspaceStaffOnboardingDataRightsCorrectionTarget?> GetAsync(
        Guid applicationId,
        CancellationToken cancellationToken) =>
        dbContext.StaffOnboardingApplications
            .AsNoTracking()
            .Where(application => application.Id == applicationId)
            .Select(application =>
                new WorkspaceStaffOnboardingDataRightsCorrectionTarget(
                    application.Id,
                    application.Version,
                    application.Status,
                    application.DisplayName,
                    application.LegalName,
                    application.WorkEmail,
                    application.WorkPhone,
                    application.EmployeeNumber,
                    application.JobTitle,
                    application.Department))
            .SingleOrDefaultAsync(cancellationToken);
}

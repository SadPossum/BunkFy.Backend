namespace BunkFy.Modules.Workspaces.Persistence.Repositories;

using BunkFy.Modules.Workspaces.Application.Ports;
using BunkFy.Modules.Workspaces.Domain;
using Microsoft.EntityFrameworkCore;

internal sealed class WorkspaceStaffAccessPlanRepository(WorkspacesDbContext dbContext)
    : IWorkspaceStaffAccessPlanRepository
{
    public Task<WorkspaceStaffAccessPlan?> GetAsync(
        Guid sourceId,
        CancellationToken cancellationToken) => dbContext.StaffAccessPlans
        .Include(plan => plan.Properties)
        .SingleOrDefaultAsync(plan => plan.Id == sourceId, cancellationToken);

    public Task AddAsync(
        WorkspaceStaffAccessPlan plan,
        CancellationToken cancellationToken)
    {
        dbContext.StaffAccessPlans.Add(plan);
        return Task.CompletedTask;
    }
}

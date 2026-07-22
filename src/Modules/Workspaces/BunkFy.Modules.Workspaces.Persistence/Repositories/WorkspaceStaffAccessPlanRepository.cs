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

    public async Task<IReadOnlyDictionary<Guid, WorkspaceStaffAccessPlan>> GetManyAsync(
        IReadOnlyCollection<Guid> sourceIds,
        CancellationToken cancellationToken)
    {
        Guid[] ids = sourceIds.Distinct().ToArray();
        if (ids.Length == 0)
        {
            return new Dictionary<Guid, WorkspaceStaffAccessPlan>();
        }

        return await dbContext.StaffAccessPlans
            .AsNoTracking()
            .Include(plan => plan.Properties)
            .Where(plan => ids.Contains(plan.Id))
            .ToDictionaryAsync(plan => plan.Id, cancellationToken)
            .ConfigureAwait(false);
    }

    public Task AddAsync(
        WorkspaceStaffAccessPlan plan,
        CancellationToken cancellationToken)
    {
        dbContext.StaffAccessPlans.Add(plan);
        return Task.CompletedTask;
    }
}

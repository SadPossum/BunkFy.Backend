namespace BunkFy.Modules.Workspaces.Application.Ports;

using BunkFy.Modules.Workspaces.Domain;

public interface IWorkspaceStaffAccessPlanRepository
{
    Task<WorkspaceStaffAccessPlan?> GetAsync(
        Guid sourceId,
        CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, WorkspaceStaffAccessPlan>> GetManyAsync(
        IReadOnlyCollection<Guid> sourceIds,
        CancellationToken cancellationToken);

    Task AddAsync(
        WorkspaceStaffAccessPlan plan,
        CancellationToken cancellationToken);
}

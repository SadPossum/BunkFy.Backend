namespace BunkFy.Modules.Workspaces.Persistence.Repositories;

using BunkFy.Modules.Properties.Contracts;
using BunkFy.Modules.Workspaces.Application.Ports;
using Microsoft.EntityFrameworkCore;

internal sealed class WorkspacePropertyProjectionRepository(WorkspacesDbContext dbContext)
    : IWorkspacePropertyProjectionRepository
{
    public async Task<bool> AreAllActiveAsync(
        IReadOnlyCollection<Guid> propertyIds,
        CancellationToken cancellationToken)
    {
        Guid[] distinctIds = propertyIds.Distinct().ToArray();
        if (distinctIds.Length == 0)
        {
            return true;
        }

        int activeCount = await dbContext.PropertyProjections.AsNoTracking()
            .CountAsync(
                property => distinctIds.Contains(property.Id) &&
                    property.Status == PropertyStatus.Active,
                cancellationToken)
            .ConfigureAwait(false);
        return activeCount == distinctIds.Length;
    }

    public async Task ApplyAsync(
        WorkspacePropertyProjectionWriteModel property,
        CancellationToken cancellationToken)
    {
        WorkspacePropertyProjection? current = await dbContext.PropertyProjections
            .FirstOrDefaultAsync(item => item.Id == property.PropertyId, cancellationToken)
            .ConfigureAwait(false);
        if (current is null)
        {
            dbContext.PropertyProjections.Add(new WorkspacePropertyProjection(
                property.ScopeId,
                property.PropertyId,
                property.Name,
                property.Status,
                property.Version));
            return;
        }

        current.Apply(property.Name, property.Status, property.Version);
    }
}

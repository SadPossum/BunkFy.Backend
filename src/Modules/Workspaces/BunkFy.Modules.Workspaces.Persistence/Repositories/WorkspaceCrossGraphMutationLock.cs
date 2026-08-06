namespace BunkFy.Modules.Workspaces.Persistence.Repositories;

using BunkFy.Modules.Workspaces.Application.Ports;
using Gma.Framework.Naming;

internal sealed class WorkspaceCrossGraphMutationLock(
    WorkspacesDbContext dbContext)
    : IWorkspaceCrossGraphMutationLock
{
    public Task AcquireAsync(CancellationToken cancellationToken)
    {
        if (!dbContext.ScopeFilterEnabled ||
            !TenantIds.TryNormalize(
                dbContext.CurrentScopeId,
                out _))
        {
            throw new WorkspaceOperationalMutationRejectedException();
        }

        return dbContext.AcquireExclusiveOperationalMutationAdmissionAsync(
            cancellationToken);
    }
}

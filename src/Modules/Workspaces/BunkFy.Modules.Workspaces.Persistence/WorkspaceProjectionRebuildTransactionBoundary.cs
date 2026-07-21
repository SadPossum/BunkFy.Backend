namespace BunkFy.Modules.Workspaces.Persistence;

using BunkFy.Modules.Workspaces.Contracts;
using Gma.Framework.ProjectionRebuild.EntityFrameworkCore;

internal sealed class WorkspaceProjectionRebuildTransactionBoundary(WorkspacesDbContext dbContext)
    : EfProjectionRebuildTransactionBoundary<WorkspacesDbContext>(
        dbContext,
        WorkspacesModuleMetadata.Name);

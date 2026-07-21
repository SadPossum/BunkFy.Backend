namespace BunkFy.Modules.Workspaces.Persistence;

using BunkFy.Modules.Workspaces.Contracts;
using Gma.Framework.ProjectionRebuild.EntityFrameworkCore;

internal sealed class WorkspaceProjectionRebuildCheckpointStore(WorkspacesDbContext dbContext)
    : EfProjectionRebuildCheckpointStore<WorkspacesDbContext, WorkspaceProjectionRebuildCheckpoint>(
        dbContext,
        WorkspacesModuleMetadata.Name,
        scopeAware: true,
        WorkspaceProjectionRebuildCheckpoint.CreateEmpty);

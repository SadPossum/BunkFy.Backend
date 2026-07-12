namespace BunkFy.Modules.Ingestion.Persistence;

using Gma.Framework.ProjectionRebuild;
using Gma.Framework.ProjectionRebuild.EntityFrameworkCore;
using BunkFy.Modules.Ingestion.Contracts;

internal sealed class IngestionProjectionRebuildCheckpointStore(IngestionDbContext dbContext)
    : EfProjectionRebuildCheckpointStore<IngestionDbContext, IngestionProjectionRebuildCheckpoint>(
        dbContext,
        IngestionModuleMetadata.Name,
        scopeAware: true,
        IngestionProjectionRebuildCheckpoint.CreateEmpty);

namespace BunkFy.Modules.Ingestion.Persistence;

using Gma.Framework.ProjectionRebuild.EntityFrameworkCore;
using BunkFy.Modules.Ingestion.Contracts;

internal sealed class IngestionProjectionRebuildTransactionBoundary(IngestionDbContext dbContext)
    : EfProjectionRebuildTransactionBoundary<IngestionDbContext>(dbContext, IngestionModuleMetadata.Name);

namespace BunkFy.Modules.DataRights.Persistence;

using BunkFy.Modules.DataRights.Contracts;
using Gma.Framework.ProjectionRebuild.EntityFrameworkCore;

internal sealed class DataRightsProjectionRebuildTransactionBoundary(DataRightsDbContext dbContext)
    : EfProjectionRebuildTransactionBoundary<DataRightsDbContext>(
        dbContext,
        DataRightsModuleMetadata.Name);

namespace BunkFy.Modules.DataRights.Persistence;

using BunkFy.Modules.DataRights.Contracts;
using Gma.Framework.ProjectionRebuild.EntityFrameworkCore;

internal sealed class DataRightsProjectionRebuildCheckpointStore(DataRightsDbContext dbContext)
    : EfProjectionRebuildCheckpointStore<DataRightsDbContext, DataRightsProjectionRebuildCheckpoint>(
        dbContext,
        DataRightsModuleMetadata.Name,
        scopeAware: true,
        DataRightsProjectionRebuildCheckpoint.CreateEmpty);

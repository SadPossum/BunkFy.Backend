namespace BunkFy.Modules.Guests.Persistence;

using Gma.Framework.ProjectionRebuild.EntityFrameworkCore;
using BunkFy.Modules.Guests.Contracts;

internal sealed class GuestsProjectionRebuildCheckpointStore(GuestsDbContext dbContext)
    : EfProjectionRebuildCheckpointStore<GuestsDbContext, GuestsProjectionRebuildCheckpoint>(
        dbContext,
        GuestsModuleMetadata.Name,
        scopeAware: true,
        GuestsProjectionRebuildCheckpoint.CreateEmpty);

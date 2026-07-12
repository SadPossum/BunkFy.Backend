namespace BunkFy.Modules.Staff.Persistence;

using Gma.Framework.ProjectionRebuild.EntityFrameworkCore;
using BunkFy.Modules.Staff.Contracts;

internal sealed class StaffProjectionRebuildCheckpointStore(StaffDbContext dbContext)
    : EfProjectionRebuildCheckpointStore<StaffDbContext, StaffProjectionRebuildCheckpoint>(
        dbContext, StaffModuleMetadata.Name, scopeAware: true, StaffProjectionRebuildCheckpoint.CreateEmpty);

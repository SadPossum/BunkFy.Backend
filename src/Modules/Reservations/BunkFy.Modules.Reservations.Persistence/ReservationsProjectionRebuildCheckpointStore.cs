namespace BunkFy.Modules.Reservations.Persistence;

using Gma.Framework.ProjectionRebuild;
using Gma.Framework.ProjectionRebuild.EntityFrameworkCore;
using BunkFy.Modules.Reservations.Contracts;

internal sealed class ReservationsProjectionRebuildCheckpointStore(ReservationsDbContext dbContext)
    : EfProjectionRebuildCheckpointStore<ReservationsDbContext, ReservationsProjectionRebuildCheckpoint>(
        dbContext,
        ReservationsModuleMetadata.Name,
        scopeAware: true,
        ReservationsProjectionRebuildCheckpoint.CreateEmpty);

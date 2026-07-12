namespace BunkFy.Modules.Reservations.Persistence;

using Gma.Framework.ProjectionRebuild.EntityFrameworkCore;
using BunkFy.Modules.Reservations.Contracts;

internal sealed class ReservationsProjectionRebuildTransactionBoundary(ReservationsDbContext dbContext)
    : EfProjectionRebuildTransactionBoundary<ReservationsDbContext>(dbContext, ReservationsModuleMetadata.Name);

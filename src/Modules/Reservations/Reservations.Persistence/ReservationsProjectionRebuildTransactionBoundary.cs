namespace Reservations.Persistence;

using Gma.Framework.ProjectionRebuild.EntityFrameworkCore;
using Reservations.Contracts;

internal sealed class ReservationsProjectionRebuildTransactionBoundary(ReservationsDbContext dbContext)
    : EfProjectionRebuildTransactionBoundary<ReservationsDbContext>(dbContext, ReservationsModuleMetadata.Name);

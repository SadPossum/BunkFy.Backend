namespace BunkFy.Modules.Reservations.Persistence.Repositories;

using BunkFy.Modules.Reservations.Application.Ports;
using BunkFy.Modules.Reservations.Domain.DataRights;
using Gma.Framework.ProjectionRebuild;
using Gma.Framework.Results;
using Microsoft.EntityFrameworkCore;

internal sealed class ReservationProcessingRestrictionProjectionRebuildWriter(
    ReservationsDbContext dbContext)
    : IProjectionRebuildWriter<
        ReservationProcessingRestrictionProjectionSnapshot>
{
    public async Task<ProjectionWriteResult> WriteAsync(
        ProjectionRebuildRequest request,
        IReadOnlyCollection<
            ReservationProcessingRestrictionProjectionSnapshot> snapshots,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(snapshots);
        if (request.DryRun)
        {
            return new(0, snapshots.Count);
        }

        foreach (ReservationProcessingRestrictionProjectionSnapshot snapshot in
                 snapshots)
        {
            ReservationProcessingRestrictionProjection? projection =
                await dbContext.ProcessingRestrictionProjections.FirstOrDefaultAsync(
                    item =>
                        item.PropertyId == snapshot.PropertyId &&
                        item.ReservationId == snapshot.ReservationId,
                    cancellationToken).ConfigureAwait(false);
            if (projection is null)
            {
                Result<ReservationProcessingRestrictionProjection> created =
                    ReservationProcessingRestrictionProjection.Create(
                        snapshot.TenantId,
                        snapshot.PropertyId,
                        snapshot.ReservationId,
                        snapshot.ContractVersion,
                        snapshot.LastTransitionAtUtc);
                if (created.IsFailure)
                {
                    throw new InvalidOperationException(created.Error.Code);
                }

                projection = created.Value;
                dbContext.ProcessingRestrictionProjections.Add(projection);
            }

            Result replaced = projection.ReplaceForRebuild(
                snapshot.ContractVersion,
                snapshot.Revision,
                snapshot.ActiveRestrictionCount,
                snapshot.LastTransitionAtUtc);
            if (replaced.IsFailure)
            {
                throw new InvalidOperationException(replaced.Error.Code);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new(snapshots.Count);
    }
}

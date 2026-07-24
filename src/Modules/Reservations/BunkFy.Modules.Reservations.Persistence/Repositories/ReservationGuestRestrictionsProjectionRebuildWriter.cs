namespace BunkFy.Modules.Reservations.Persistence.Repositories;

using BunkFy.Modules.Guests.Contracts;
using BunkFy.Modules.Reservations.Application.Ports;
using Gma.Framework.ProjectionRebuild;

internal sealed class ReservationGuestRestrictionsProjectionRebuildWriter(
    IReservationGuestProfileProjectionRepository profiles,
    ReservationsDbContext dbContext)
    : IProjectionRebuildWriter<GuestProcessingRestrictionProjectionExport>
{
    public async Task<ProjectionWriteResult> WriteAsync(
        ProjectionRebuildRequest request,
        IReadOnlyCollection<GuestProcessingRestrictionProjectionExport> snapshots,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(snapshots);
        if (request.DryRun)
        {
            return new(0, snapshots.Count);
        }

        foreach (GuestProcessingRestrictionProjectionExport restriction in snapshots)
        {
            await profiles.ApplyRestrictionAsync(
                new(
                    restriction.TenantId,
                    restriction.PropertyId,
                    restriction.GuestId,
                    restriction.ContractVersion,
                    restriction.ProjectionRevision,
                    restriction.IsRestricted),
                cancellationToken).ConfigureAwait(false);
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new(snapshots.Count);
    }
}

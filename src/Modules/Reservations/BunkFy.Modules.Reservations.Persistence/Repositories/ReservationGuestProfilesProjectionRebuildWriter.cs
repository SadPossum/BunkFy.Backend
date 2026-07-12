namespace BunkFy.Modules.Reservations.Persistence.Repositories;

using Gma.Framework.ProjectionRebuild;
using BunkFy.Modules.Guests.Contracts;
using BunkFy.Modules.Reservations.Application.Ports;

internal sealed class ReservationGuestProfilesProjectionRebuildWriter(
    IReservationGuestProfileProjectionRepository profiles,
    ReservationsDbContext dbContext)
    : IProjectionRebuildWriter<GuestProfileEligibilityProjectionExport>
{
    public async Task<ProjectionWriteResult> WriteAsync(
        ProjectionRebuildRequest request,
        IReadOnlyCollection<GuestProfileEligibilityProjectionExport> snapshots,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(snapshots);
        if (request.DryRun)
        {
            return new(0, snapshots.Count);
        }

        foreach (GuestProfileEligibilityProjectionExport profile in snapshots)
        {
            await profiles.ApplyAsync(
                new(
                    profile.TenantId,
                    profile.GuestId,
                    profile.OriginPropertyId,
                    profile.Status,
                    profile.GuestVersion),
                cancellationToken).ConfigureAwait(false);
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new(snapshots.Count);
    }
}

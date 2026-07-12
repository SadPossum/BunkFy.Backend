namespace BunkFy.Modules.Guests.Persistence.Repositories;

using Gma.Framework.ProjectionRebuild;
using BunkFy.Modules.Guests.Application.Ports;
using BunkFy.Modules.Guests.Contracts;

internal sealed class GuestStayHistoryProjectionRebuildWriter(
    IGuestStayHistoryRepository stays,
    GuestsDbContext dbContext)
    : IProjectionRebuildWriter<ReservationGuestStayProjectionExport>
{
    public async Task<ProjectionWriteResult> WriteAsync(
        ProjectionRebuildRequest request,
        IReadOnlyCollection<ReservationGuestStayProjectionExport> snapshots,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(snapshots);
        if (request.DryRun)
        {
            return new(0, snapshots.Count);
        }

        foreach (ReservationGuestStayProjectionExport stay in snapshots)
        {
            await stays.ApplyAsync(
                new(
                    stay.TenantId,
                    stay.GuestId,
                    stay.ReservationId,
                    stay.PropertyId,
                    stay.Role,
                    stay.Arrival,
                    stay.Departure,
                    stay.Status,
                    stay.CheckedInBusinessDate,
                    stay.NoShowBusinessDate,
                    stay.CheckedOutBusinessDate,
                    stay.IsCurrentParticipant,
                    stay.ReservationVersion),
                cancellationToken).ConfigureAwait(false);
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new(snapshots.Count);
    }
}

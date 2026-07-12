namespace BunkFy.Modules.Ingestion.Persistence.Repositories;

using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Domain.Reservations;
using Microsoft.EntityFrameworkCore;

internal sealed class ReservationSourceLinkRepository(IngestionDbContext dbContext)
    : IReservationSourceLinkRepository
{
    public Task<ReservationSourceLink?> GetAsync(Guid sourceLinkId, CancellationToken cancellationToken) =>
        dbContext.ReservationSourceLinks.FirstOrDefaultAsync(link => link.Id == sourceLinkId, cancellationToken);

    public Task<ReservationSourceLink?> FindBySourceAsync(
        Guid connectionId,
        string sourceReference,
        CancellationToken cancellationToken) =>
        dbContext.ReservationSourceLinks.FirstOrDefaultAsync(
            link => link.ConnectionId == connectionId && link.SourceReference == sourceReference,
            cancellationToken);

    public Task<ReservationSourceLink?> FindByReservationAsync(Guid reservationId, CancellationToken cancellationToken) =>
        dbContext.ReservationSourceLinks.FirstOrDefaultAsync(link => link.ReservationId == reservationId, cancellationToken);

    public Task AddAsync(ReservationSourceLink sourceLink, CancellationToken cancellationToken)
    {
        dbContext.ReservationSourceLinks.Add(sourceLink);
        return Task.CompletedTask;
    }
}

namespace BunkFy.Modules.Reservations.Persistence.Repositories;

using BunkFy.Modules.Reservations.Application.Ports;
using BunkFy.Modules.Reservations.Domain.GuestRecords;
using Microsoft.EntityFrameworkCore;

internal sealed class ReservationGuestRecordLinkProcessRepository(
    ReservationsDbContext dbContext)
    : IReservationGuestRecordLinkProcessRepository
{
    public Task<ReservationGuestRecordLinkProcess?> GetByOperationAsync(
        Guid operationId,
        CancellationToken cancellationToken) =>
        dbContext.GuestRecordLinkProcesses.FirstOrDefaultAsync(
            process => process.Id == operationId,
            cancellationToken);

    public Task<ReservationGuestRecordLinkProcess?> GetByReservationAsync(
        Guid propertyId,
        Guid reservationId,
        CancellationToken cancellationToken) =>
        dbContext.GuestRecordLinkProcesses.FirstOrDefaultAsync(
            process =>
                process.PropertyId == propertyId &&
                process.ReservationId == reservationId,
            cancellationToken);

    public Task<ReservationGuestRecordLinkProcess?> GetByConfirmationAsync(
        Guid creationConfirmationId,
        CancellationToken cancellationToken) =>
        dbContext.GuestRecordLinkProcesses.AsNoTracking().FirstOrDefaultAsync(
            process => process.CreationConfirmationId == creationConfirmationId,
            cancellationToken);

    public Task AddAsync(
        ReservationGuestRecordLinkProcess process,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(process);
        dbContext.GuestRecordLinkProcesses.Add(process);
        return Task.CompletedTask;
    }
}

namespace BunkFy.Modules.Reservations.Application.Ports;

using BunkFy.Modules.Reservations.Domain.GuestRecords;

public interface IReservationGuestRecordLinkProcessRepository
{
    Task<ReservationGuestRecordLinkProcess?> GetByOperationAsync(
        Guid operationId,
        CancellationToken cancellationToken);

    Task<ReservationGuestRecordLinkProcess?> GetByReservationAsync(
        Guid propertyId,
        Guid reservationId,
        CancellationToken cancellationToken);

    Task<ReservationGuestRecordLinkProcess?> GetByConfirmationAsync(
        Guid creationConfirmationId,
        CancellationToken cancellationToken);

    Task AddAsync(
        ReservationGuestRecordLinkProcess process,
        CancellationToken cancellationToken);
}

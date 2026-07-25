namespace BunkFy.Modules.Reservations.Application.Ports;

using BunkFy.Modules.Reservations.Contracts;
using BunkFy.Modules.Reservations.Domain.DataRights;
using Gma.Framework.Pagination;

public interface IReservationDataHoldRepository
{
    Task<ReservationDataHoldReceipt?> FindReceiptByIdempotencyKeyAsync(
        Guid idempotencyKey,
        CancellationToken cancellationToken);

    Task<ReservationDataHold?> GetAsync(
        Guid propertyId,
        Guid reservationId,
        Guid holdId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<ReservationDataHold>> ListAsync(
        Guid propertyId,
        Guid reservationId,
        ReservationDataHoldStatus? status,
        PageRequest pageRequest,
        CancellationToken cancellationToken);

    Task AddAsync(
        ReservationDataHold hold,
        CancellationToken cancellationToken);

    Task AddReceiptAsync(
        ReservationDataHoldReceipt receipt,
        CancellationToken cancellationToken);
}

namespace BunkFy.Modules.Reservations.Application.Ports;

using BunkFy.Modules.Reservations.Domain.DataRights;

public interface IReservationDataRightsCorrectionReceiptRepository
{
    Task<ReservationDataRightsCorrectionReceipt?> FindByIdempotencyKeyAsync(
        Guid idempotencyKey,
        CancellationToken cancellationToken);

    Task AddAsync(
        ReservationDataRightsCorrectionReceipt receipt,
        CancellationToken cancellationToken);
}

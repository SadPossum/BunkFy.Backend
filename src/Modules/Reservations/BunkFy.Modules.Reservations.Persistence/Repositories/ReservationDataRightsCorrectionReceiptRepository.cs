namespace BunkFy.Modules.Reservations.Persistence.Repositories;

using BunkFy.Modules.Reservations.Application.Ports;
using BunkFy.Modules.Reservations.Domain.DataRights;
using Microsoft.EntityFrameworkCore;

internal sealed class ReservationDataRightsCorrectionReceiptRepository(
    ReservationsDbContext dbContext)
    : IReservationDataRightsCorrectionReceiptRepository
{
    public Task<ReservationDataRightsCorrectionReceipt?> FindByIdempotencyKeyAsync(
        Guid idempotencyKey,
        CancellationToken cancellationToken) =>
        dbContext.DataRightsCorrectionReceipts.FirstOrDefaultAsync(
            receipt => receipt.IdempotencyKey == idempotencyKey,
            cancellationToken);

    public Task AddAsync(
        ReservationDataRightsCorrectionReceipt receipt,
        CancellationToken cancellationToken)
    {
        dbContext.DataRightsCorrectionReceipts.Add(receipt);
        return Task.CompletedTask;
    }
}

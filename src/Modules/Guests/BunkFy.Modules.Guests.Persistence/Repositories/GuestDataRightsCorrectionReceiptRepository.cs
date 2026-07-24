namespace BunkFy.Modules.Guests.Persistence.Repositories;

using BunkFy.Modules.Guests.Application.Ports;
using BunkFy.Modules.Guests.Domain.DataRights;
using Microsoft.EntityFrameworkCore;

internal sealed class GuestDataRightsCorrectionReceiptRepository(GuestsDbContext dbContext)
    : IGuestDataRightsCorrectionReceiptRepository
{
    public Task<GuestDataRightsCorrectionReceipt?> FindByIdempotencyKeyAsync(
        Guid idempotencyKey,
        CancellationToken cancellationToken) =>
        dbContext.DataRightsCorrectionReceipts.FirstOrDefaultAsync(
            receipt => receipt.IdempotencyKey == idempotencyKey,
            cancellationToken);

    public Task AddAsync(
        GuestDataRightsCorrectionReceipt receipt,
        CancellationToken cancellationToken)
    {
        dbContext.DataRightsCorrectionReceipts.Add(receipt);
        return Task.CompletedTask;
    }
}

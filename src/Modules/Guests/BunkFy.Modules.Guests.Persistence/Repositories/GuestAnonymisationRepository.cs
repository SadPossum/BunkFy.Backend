namespace BunkFy.Modules.Guests.Persistence.Repositories;

using BunkFy.Modules.Guests.Application.Ports;
using BunkFy.Modules.Guests.Domain.DataRights;
using Microsoft.EntityFrameworkCore;

internal sealed class GuestAnonymisationRepository(GuestsDbContext dbContext)
    : IGuestAnonymisationRepository
{
    public Task<GuestAnonymisationReceipt?> FindReceiptByIdempotencyKeyAsync(
        Guid idempotencyKey,
        CancellationToken cancellationToken) =>
        dbContext.AnonymisationReceipts
            .AsNoTracking()
            .FirstOrDefaultAsync(
                receipt => receipt.IdempotencyKey == idempotencyKey,
                cancellationToken);

    public Task<GuestAnonymisationTombstone?> GetTombstoneAsync(
        Guid guestId,
        CancellationToken cancellationToken) =>
        dbContext.AnonymisationTombstones
            .AsNoTracking()
            .FirstOrDefaultAsync(
                tombstone => tombstone.Id == guestId,
                cancellationToken);

    public Task AddAsync(
        GuestAnonymisationReceipt receipt,
        GuestAnonymisationTombstone tombstone,
        CancellationToken cancellationToken)
    {
        dbContext.AnonymisationReceipts.Add(receipt);
        dbContext.AnonymisationTombstones.Add(tombstone);
        return Task.CompletedTask;
    }
}

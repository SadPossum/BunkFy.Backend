namespace BunkFy.Modules.Guests.Persistence.Repositories;

using BunkFy.Modules.Guests.Application.Ports;
using BunkFy.Modules.Guests.Domain.Aggregates;
using BunkFy.Modules.Guests.Domain.DataRights;
using Microsoft.EntityFrameworkCore;

internal sealed class GuestAnonymisationRestoreRepository(
    GuestsDbContext dbContext)
    : IGuestAnonymisationRestoreRepository
{
    public Task<GuestProfile?> GetProfileAsync(
        Guid guestId,
        CancellationToken cancellationToken) =>
        dbContext.GuestProfiles.SingleOrDefaultAsync(
            profile => profile.Id == guestId,
            cancellationToken);

    public Task<GuestAnonymisationTombstone?> GetTombstoneAsync(
        Guid guestId,
        CancellationToken cancellationToken) =>
        dbContext.AnonymisationTombstones.SingleOrDefaultAsync(
            tombstone => tombstone.Id == guestId,
            cancellationToken);

    public Task<GuestAnonymisationRestoreReceipt?> GetReceiptAsync(
        Guid ledgerEntryId,
        CancellationToken cancellationToken) =>
        dbContext.AnonymisationRestoreReceipts
            .AsNoTracking()
            .SingleOrDefaultAsync(
                receipt => receipt.LedgerEntryId == ledgerEntryId,
                cancellationToken);

    public Task AddAsync(
        GuestAnonymisationRestoreReceipt receipt,
        GuestAnonymisationTombstone? newTombstone,
        GuestProfile? newProfile,
        CancellationToken cancellationToken)
    {
        if (newProfile is not null)
        {
            dbContext.GuestProfiles.Add(newProfile);
        }

        dbContext.AnonymisationRestoreReceipts.Add(receipt);
        if (newTombstone is not null)
        {
            dbContext.AnonymisationTombstones.Add(newTombstone);
        }

        return Task.CompletedTask;
    }
}

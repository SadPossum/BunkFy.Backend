namespace BunkFy.Modules.Staff.Persistence.Repositories;

using BunkFy.Modules.Staff.Application.Ports;
using BunkFy.Modules.Staff.Domain.DataRights;
using Microsoft.EntityFrameworkCore;

internal sealed class StaffAnonymisationRestoreRepository(
    StaffDbContext dbContext)
    : IStaffAnonymisationRestoreRepository
{
    public Task<StaffAnonymisationTombstone?> GetTombstoneAsync(
        Guid staffMemberId,
        CancellationToken cancellationToken) =>
        dbContext.AnonymisationTombstones.SingleOrDefaultAsync(
            tombstone => tombstone.Id == staffMemberId,
            cancellationToken);

    public Task<StaffAnonymisationRestoreReceipt?> GetReceiptAsync(
        Guid ledgerEntryId,
        CancellationToken cancellationToken) =>
        dbContext.AnonymisationRestoreReceipts
            .AsNoTracking()
            .SingleOrDefaultAsync(
                receipt => receipt.LedgerEntryId == ledgerEntryId,
                cancellationToken);

    public Task AddAsync(
        StaffAnonymisationRestoreReceipt receipt,
        StaffAnonymisationTombstone? newTombstone,
        CancellationToken cancellationToken)
    {
        dbContext.AnonymisationRestoreReceipts.Add(receipt);
        if (newTombstone is not null)
        {
            dbContext.AnonymisationTombstones.Add(newTombstone);
        }

        return Task.CompletedTask;
    }
}

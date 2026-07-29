namespace BunkFy.Modules.Staff.Persistence.Repositories;

using BunkFy.Modules.Staff.Application.Ports;
using BunkFy.Modules.Staff.Domain.DataRights;
using Microsoft.EntityFrameworkCore;

internal sealed class StaffAnonymisationRepository(
    StaffDbContext dbContext)
    : IStaffAnonymisationRepository
{
    public Task<StaffAnonymisationReceipt?>
        FindReceiptByIdempotencyKeyAsync(
            Guid idempotencyKey,
            CancellationToken cancellationToken) =>
        dbContext.AnonymisationReceipts
            .AsNoTracking()
            .FirstOrDefaultAsync(
                receipt =>
                    receipt.IdempotencyKey == idempotencyKey,
                cancellationToken);

    public Task<StaffAnonymisationTombstone?> GetTombstoneAsync(
        Guid staffMemberId,
        CancellationToken cancellationToken) =>
        dbContext.AnonymisationTombstones
            .AsNoTracking()
            .FirstOrDefaultAsync(
                tombstone => tombstone.Id == staffMemberId,
                cancellationToken);

    public Task AddAsync(
        StaffAnonymisationReceipt receipt,
        StaffAnonymisationTombstone tombstone,
        CancellationToken cancellationToken)
    {
        dbContext.AnonymisationReceipts.Add(receipt);
        dbContext.AnonymisationTombstones.Add(tombstone);
        return Task.CompletedTask;
    }
}

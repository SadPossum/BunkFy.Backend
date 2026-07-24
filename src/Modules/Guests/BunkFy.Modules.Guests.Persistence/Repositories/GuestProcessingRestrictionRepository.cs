namespace BunkFy.Modules.Guests.Persistence.Repositories;

using BunkFy.Modules.Guests.Application.Ports;
using BunkFy.Modules.Guests.Domain.DataRights;
using BunkFy.Modules.Guests.Domain.Models;
using Microsoft.EntityFrameworkCore;

internal sealed class GuestProcessingRestrictionRepository(GuestsDbContext dbContext)
    : IGuestProcessingRestrictionRepository
{
    public Task<GuestProcessingRestrictionReceipt?> FindReceiptByIdempotencyKeyAsync(
        Guid idempotencyKey,
        CancellationToken cancellationToken) =>
        dbContext.ProcessingRestrictionReceipts.AsNoTracking().FirstOrDefaultAsync(
            receipt => receipt.IdempotencyKey == idempotencyKey,
            cancellationToken);

    public Task<GuestProcessingRestriction?> FindByApplyApprovalAsync(
        Guid propertyId,
        Guid guestId,
        Guid caseId,
        long approvalRevision,
        CancellationToken cancellationToken) =>
        dbContext.ProcessingRestrictions.AsNoTracking().FirstOrDefaultAsync(
            restriction =>
                restriction.PropertyId == propertyId &&
                restriction.GuestId == guestId &&
                restriction.ApplyCaseId == caseId &&
                restriction.ApplyApprovalRevision == approvalRevision,
            cancellationToken);

    public Task<GuestProcessingRestriction?> GetAsync(
        Guid propertyId,
        Guid restrictionId,
        CancellationToken cancellationToken) =>
        dbContext.ProcessingRestrictions.FirstOrDefaultAsync(
            restriction =>
                restriction.Id == restrictionId &&
                restriction.PropertyId == propertyId,
            cancellationToken);

    public async Task<IReadOnlyCollection<GuestProcessingRestriction>> ListActiveAsync(
        Guid propertyId,
        Guid guestId,
        CancellationToken cancellationToken) =>
        await dbContext.ProcessingRestrictions.AsNoTracking()
            .Where(restriction =>
                restriction.PropertyId == propertyId &&
                restriction.GuestId == guestId &&
                restriction.Status == GuestProcessingRestrictionState.Active)
            .OrderBy(restriction => restriction.AppliedAtUtc)
            .ThenBy(restriction => restriction.Id)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

    public Task AddAsync(
        GuestProcessingRestriction restriction,
        CancellationToken cancellationToken)
    {
        dbContext.ProcessingRestrictions.Add(restriction);
        return Task.CompletedTask;
    }

    public Task AddReceiptAsync(
        GuestProcessingRestrictionReceipt receipt,
        CancellationToken cancellationToken)
    {
        dbContext.ProcessingRestrictionReceipts.Add(receipt);
        return Task.CompletedTask;
    }
}

namespace BunkFy.Modules.Staff.Persistence.Repositories;

using BunkFy.Modules.Staff.Application.Ports;
using BunkFy.Modules.Staff.Domain.DataRights;
using BunkFy.Modules.Staff.Domain.Models;
using Gma.Framework.Pagination;
using Microsoft.EntityFrameworkCore;

internal sealed class StaffProcessingRestrictionRepository(
    StaffDbContext dbContext)
    : IStaffProcessingRestrictionRepository
{
    public Task<StaffProcessingRestrictionReceipt?>
        FindReceiptByIdempotencyKeyAsync(
            Guid idempotencyKey,
            CancellationToken cancellationToken) =>
        dbContext.ProcessingRestrictionReceipts
            .AsNoTracking()
            .FirstOrDefaultAsync(
                receipt => receipt.IdempotencyKey == idempotencyKey,
                cancellationToken);

    public Task<StaffProcessingRestriction?> FindByApplyApprovalAsync(
        Guid staffMemberId,
        Guid caseId,
        long approvalRevision,
        CancellationToken cancellationToken) =>
        dbContext.ProcessingRestrictions
            .AsNoTracking()
            .FirstOrDefaultAsync(
                restriction =>
                    restriction.StaffMemberId == staffMemberId &&
                    restriction.ApplyCaseId == caseId &&
                    restriction.ApplyApprovalRevision == approvalRevision,
                cancellationToken);

    public Task<StaffProcessingRestriction?> FindByReleaseApprovalAsync(
        Guid staffMemberId,
        Guid caseId,
        long approvalRevision,
        CancellationToken cancellationToken) =>
        dbContext.ProcessingRestrictions
            .AsNoTracking()
            .FirstOrDefaultAsync(
                restriction =>
                    restriction.StaffMemberId == staffMemberId &&
                    restriction.ReleaseCaseId == caseId &&
                    restriction.ReleaseApprovalRevision == approvalRevision,
                cancellationToken);

    public Task<StaffProcessingRestriction?> GetAsync(
        Guid restrictionId,
        CancellationToken cancellationToken) =>
        dbContext.ProcessingRestrictions.FirstOrDefaultAsync(
            restriction => restriction.Id == restrictionId,
            cancellationToken);

    public async Task<IReadOnlyCollection<StaffProcessingRestriction>>
        ListActiveAsync(
            Guid staffMemberId,
            PageRequest pageRequest,
            CancellationToken cancellationToken) =>
        await dbContext.ProcessingRestrictions
            .AsNoTracking()
            .Where(restriction =>
                restriction.StaffMemberId == staffMemberId &&
                restriction.Status ==
                    StaffProcessingRestrictionState.Active)
            .OrderBy(restriction => restriction.AppliedAtUtc)
            .ThenBy(restriction => restriction.Id)
            .Skip(pageRequest.SkipCount)
            .Take(pageRequest.PageSize)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

    public Task AddAsync(
        StaffProcessingRestriction restriction,
        CancellationToken cancellationToken)
    {
        dbContext.ProcessingRestrictions.Add(restriction);
        return Task.CompletedTask;
    }

    public Task AddReceiptAsync(
        StaffProcessingRestrictionReceipt receipt,
        CancellationToken cancellationToken)
    {
        dbContext.ProcessingRestrictionReceipts.Add(receipt);
        return Task.CompletedTask;
    }
}

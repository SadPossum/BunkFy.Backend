namespace BunkFy.Modules.Workspaces.Persistence.Repositories;

using BunkFy.Modules.Workspaces.Application.Ports;
using BunkFy.Modules.Workspaces.Domain.DataRights;
using Gma.Framework.Pagination;
using Microsoft.EntityFrameworkCore;

internal sealed class
    WorkspaceStaffOnboardingProcessingRestrictionRepository(
        WorkspacesDbContext dbContext)
    : IWorkspaceStaffOnboardingProcessingRestrictionRepository
{
    public Task<WorkspaceStaffOnboardingProcessingRestrictionReceipt?>
        FindReceiptByIdempotencyKeyAsync(
            Guid idempotencyKey,
            CancellationToken cancellationToken) =>
        dbContext.StaffOnboardingProcessingRestrictionReceipts
            .AsNoTracking()
            .FirstOrDefaultAsync(
                receipt => receipt.IdempotencyKey == idempotencyKey,
                cancellationToken);

    public Task<WorkspaceStaffOnboardingProcessingRestriction?>
        FindByApplyApprovalAsync(
            Guid applicationId,
            Guid caseId,
            long approvalRevision,
            CancellationToken cancellationToken) =>
        dbContext.StaffOnboardingProcessingRestrictions
            .AsNoTracking()
            .FirstOrDefaultAsync(
                restriction =>
                    restriction.ApplicationId == applicationId &&
                    restriction.ApplyCaseId == caseId &&
                    restriction.ApplyApprovalRevision == approvalRevision,
                cancellationToken);

    public Task<WorkspaceStaffOnboardingProcessingRestriction?>
        FindByReleaseApprovalAsync(
            Guid applicationId,
            Guid caseId,
            long approvalRevision,
            CancellationToken cancellationToken) =>
        dbContext.StaffOnboardingProcessingRestrictions
            .AsNoTracking()
            .FirstOrDefaultAsync(
                restriction =>
                    restriction.ApplicationId == applicationId &&
                    restriction.ReleaseCaseId == caseId &&
                    restriction.ReleaseApprovalRevision == approvalRevision,
                cancellationToken);

    public Task<WorkspaceStaffOnboardingProcessingRestriction?> GetAsync(
        Guid restrictionId,
        CancellationToken cancellationToken) =>
        dbContext.StaffOnboardingProcessingRestrictions.FirstOrDefaultAsync(
            restriction => restriction.Id == restrictionId,
            cancellationToken);

    public async Task<IReadOnlyCollection<
        WorkspaceStaffOnboardingProcessingRestriction>> ListActiveAsync(
        Guid applicationId,
        PageRequest pageRequest,
        CancellationToken cancellationToken) =>
        await dbContext.StaffOnboardingProcessingRestrictions
            .AsNoTracking()
            .Where(restriction =>
                restriction.ApplicationId == applicationId &&
                restriction.Status ==
                    WorkspaceStaffOnboardingProcessingRestrictionState.Active)
            .OrderBy(restriction => restriction.AppliedAtUtc)
            .ThenBy(restriction => restriction.Id)
            .Skip(pageRequest.SkipCount)
            .Take(pageRequest.PageSize)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

    public Task AddAsync(
        WorkspaceStaffOnboardingProcessingRestriction restriction,
        CancellationToken cancellationToken)
    {
        dbContext.StaffOnboardingProcessingRestrictions.Add(restriction);
        return Task.CompletedTask;
    }

    public Task AddReceiptAsync(
        WorkspaceStaffOnboardingProcessingRestrictionReceipt receipt,
        CancellationToken cancellationToken)
    {
        dbContext.StaffOnboardingProcessingRestrictionReceipts.Add(receipt);
        return Task.CompletedTask;
    }
}

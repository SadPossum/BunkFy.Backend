namespace BunkFy.Modules.Workspaces.Application.Ports;

using BunkFy.Modules.Workspaces.Domain.DataRights;
using Gma.Framework.Pagination;

public interface IWorkspaceStaffOnboardingProcessingRestrictionRepository
{
    Task<WorkspaceStaffOnboardingProcessingRestrictionReceipt?>
        FindReceiptByIdempotencyKeyAsync(
            Guid idempotencyKey,
            CancellationToken cancellationToken);

    Task<WorkspaceStaffOnboardingProcessingRestriction?>
        FindByApplyApprovalAsync(
            Guid applicationId,
            Guid caseId,
            long approvalRevision,
            CancellationToken cancellationToken);

    Task<WorkspaceStaffOnboardingProcessingRestriction?>
        FindByReleaseApprovalAsync(
            Guid applicationId,
            Guid caseId,
            long approvalRevision,
            CancellationToken cancellationToken);

    Task<WorkspaceStaffOnboardingProcessingRestriction?> GetAsync(
        Guid restrictionId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<
        WorkspaceStaffOnboardingProcessingRestriction>> ListActiveAsync(
        Guid applicationId,
        PageRequest pageRequest,
        CancellationToken cancellationToken);

    Task AddAsync(
        WorkspaceStaffOnboardingProcessingRestriction restriction,
        CancellationToken cancellationToken);

    Task AddReceiptAsync(
        WorkspaceStaffOnboardingProcessingRestrictionReceipt receipt,
        CancellationToken cancellationToken);
}

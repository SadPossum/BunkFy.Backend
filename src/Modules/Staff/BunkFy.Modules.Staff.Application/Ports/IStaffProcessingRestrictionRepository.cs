namespace BunkFy.Modules.Staff.Application.Ports;

using BunkFy.Modules.Staff.Domain.DataRights;
using Gma.Framework.Pagination;

public interface IStaffProcessingRestrictionRepository
{
    Task<StaffProcessingRestrictionReceipt?> FindReceiptByIdempotencyKeyAsync(
        Guid idempotencyKey,
        CancellationToken cancellationToken);

    Task<StaffProcessingRestriction?> FindByApplyApprovalAsync(
        Guid staffMemberId,
        Guid caseId,
        long approvalRevision,
        CancellationToken cancellationToken);

    Task<StaffProcessingRestriction?> FindByReleaseApprovalAsync(
        Guid staffMemberId,
        Guid caseId,
        long approvalRevision,
        CancellationToken cancellationToken);

    Task<StaffProcessingRestriction?> GetAsync(
        Guid restrictionId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<StaffProcessingRestriction>> ListActiveAsync(
        Guid staffMemberId,
        PageRequest pageRequest,
        CancellationToken cancellationToken);

    Task AddAsync(
        StaffProcessingRestriction restriction,
        CancellationToken cancellationToken);

    Task AddReceiptAsync(
        StaffProcessingRestrictionReceipt receipt,
        CancellationToken cancellationToken);
}

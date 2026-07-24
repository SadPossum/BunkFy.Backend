namespace BunkFy.Modules.Guests.Application.Ports;

using BunkFy.Modules.Guests.Domain.DataRights;

public interface IGuestProcessingRestrictionRepository
{
    Task<GuestProcessingRestrictionReceipt?> FindReceiptByIdempotencyKeyAsync(
        Guid idempotencyKey,
        CancellationToken cancellationToken);

    Task<GuestProcessingRestriction?> FindByApplyApprovalAsync(
        Guid propertyId,
        Guid guestId,
        Guid caseId,
        long approvalRevision,
        CancellationToken cancellationToken);

    Task<GuestProcessingRestriction?> GetAsync(
        Guid propertyId,
        Guid restrictionId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<GuestProcessingRestriction>> ListActiveAsync(
        Guid propertyId,
        Guid guestId,
        CancellationToken cancellationToken);

    Task AddAsync(
        GuestProcessingRestriction restriction,
        CancellationToken cancellationToken);

    Task AddReceiptAsync(
        GuestProcessingRestrictionReceipt receipt,
        CancellationToken cancellationToken);
}

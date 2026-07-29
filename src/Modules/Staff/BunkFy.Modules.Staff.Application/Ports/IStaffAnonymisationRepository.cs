namespace BunkFy.Modules.Staff.Application.Ports;

using BunkFy.Modules.Staff.Domain.DataRights;

public interface IStaffAnonymisationRepository
{
    Task<StaffAnonymisationReceipt?> FindReceiptByIdempotencyKeyAsync(
        Guid idempotencyKey,
        CancellationToken cancellationToken);

    Task<StaffAnonymisationTombstone?> GetTombstoneAsync(
        Guid staffMemberId,
        CancellationToken cancellationToken);

    Task AddAsync(
        StaffAnonymisationReceipt receipt,
        StaffAnonymisationTombstone tombstone,
        CancellationToken cancellationToken);
}

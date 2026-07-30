namespace BunkFy.Modules.Staff.Application.Ports;

using BunkFy.Modules.Staff.Domain.DataRights;

public interface IStaffAnonymisationRestoreRepository
{
    Task<StaffAnonymisationTombstone?> GetTombstoneAsync(
        Guid staffMemberId,
        CancellationToken cancellationToken);

    Task<StaffAnonymisationRestoreReceipt?> GetReceiptAsync(
        Guid ledgerEntryId,
        CancellationToken cancellationToken);

    Task AddAsync(
        StaffAnonymisationRestoreReceipt receipt,
        StaffAnonymisationTombstone? newTombstone,
        CancellationToken cancellationToken);
}

namespace BunkFy.Modules.Staff.Application.Ports;

using BunkFy.Modules.Staff.Domain.Governance;

public interface IStaffEmploymentGovernanceRepository
{
    Task<StaffEmploymentGovernance?> GetAsync(
        Guid staffMemberId,
        CancellationToken cancellationToken);

    Task<StaffEmploymentGovernanceChangeReceipt?>
        FindReceiptByIdempotencyKeyAsync(
            Guid idempotencyKey,
            CancellationToken cancellationToken);

    Task AddAsync(
        StaffEmploymentGovernance governance,
        CancellationToken cancellationToken);

    Task AddReceiptAsync(
        StaffEmploymentGovernanceChangeReceipt receipt,
        CancellationToken cancellationToken);
}

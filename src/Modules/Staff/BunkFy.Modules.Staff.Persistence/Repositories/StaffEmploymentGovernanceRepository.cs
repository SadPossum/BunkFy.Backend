namespace BunkFy.Modules.Staff.Persistence.Repositories;

using BunkFy.Modules.Staff.Application.Ports;
using BunkFy.Modules.Staff.Domain.Governance;
using Microsoft.EntityFrameworkCore;

internal sealed class StaffEmploymentGovernanceRepository(
    StaffDbContext dbContext)
    : IStaffEmploymentGovernanceRepository
{
    public Task<StaffEmploymentGovernance?> GetAsync(
        Guid staffMemberId,
        CancellationToken cancellationToken) =>
        dbContext.Set<StaffEmploymentGovernance>()
            .Include(
                governance =>
                    governance.AcceptedAcknowledgements)
            .SingleOrDefaultAsync(
                governance =>
                    governance.Id == staffMemberId,
                cancellationToken);

    public Task<StaffEmploymentGovernanceChangeReceipt?>
        FindReceiptByIdempotencyKeyAsync(
            Guid idempotencyKey,
            CancellationToken cancellationToken) =>
        dbContext.Set<StaffEmploymentGovernanceChangeReceipt>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                receipt =>
                    receipt.IdempotencyKey == idempotencyKey,
                cancellationToken);

    public Task AddAsync(
        StaffEmploymentGovernance governance,
        CancellationToken cancellationToken)
    {
        dbContext.Set<StaffEmploymentGovernance>().Add(governance);
        return Task.CompletedTask;
    }

    public Task AddReceiptAsync(
        StaffEmploymentGovernanceChangeReceipt receipt,
        CancellationToken cancellationToken)
    {
        dbContext.Set<StaffEmploymentGovernanceChangeReceipt>()
            .Add(receipt);
        return Task.CompletedTask;
    }
}

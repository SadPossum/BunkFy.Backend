namespace BunkFy.Modules.Staff.Persistence.Repositories;

using BunkFy.Modules.Staff.Application.Ports;
using BunkFy.Modules.Staff.Domain.DataRights;
using Microsoft.EntityFrameworkCore;

internal sealed class StaffDataRightsCorrectionReceiptRepository(
    StaffDbContext dbContext)
    : IStaffDataRightsCorrectionReceiptRepository
{
    public Task<StaffDataRightsCorrectionReceipt?> FindByExecutionIdAsync(
        Guid executionId,
        CancellationToken cancellationToken) =>
        dbContext.DataRightsCorrectionReceipts.FirstOrDefaultAsync(
            receipt => receipt.ExecutionId == executionId,
            cancellationToken);

    public Task AddAsync(
        StaffDataRightsCorrectionReceipt receipt,
        CancellationToken cancellationToken)
    {
        dbContext.DataRightsCorrectionReceipts.Add(receipt);
        return Task.CompletedTask;
    }
}

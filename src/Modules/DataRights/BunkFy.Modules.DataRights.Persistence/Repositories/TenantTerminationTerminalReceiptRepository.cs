namespace BunkFy.Modules.DataRights.Persistence.Repositories;

using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;

internal sealed class TenantTerminationTerminalReceiptRepository(
    DataRightsDbContext dbContext)
    : ITenantTerminationTerminalReceiptRepository
{
    public Task AddAsync(
        TenantTerminationTerminalReceipt receipt,
        CancellationToken cancellationToken)
    {
        dbContext.TenantTerminationTerminalReceipts.Add(receipt);
        return Task.CompletedTask;
    }

    public Task<TenantTerminationTerminalReceipt?> GetAsync(
        Guid receiptId,
        CancellationToken cancellationToken) =>
        dbContext.TenantTerminationTerminalReceipts.SingleOrDefaultAsync(
            receipt => receipt.Id == receiptId,
            cancellationToken);

    public Task<TenantTerminationTerminalReceipt?> GetByIdempotencyKeyAsync(
        Guid idempotencyKey,
        CancellationToken cancellationToken) =>
        dbContext.TenantTerminationTerminalReceipts.SingleOrDefaultAsync(
            receipt => receipt.IdempotencyKey == idempotencyKey,
            cancellationToken);

    public Task<TenantTerminationTerminalReceipt?> GetByProcessAsync(
        Guid processId,
        long verificationOperationRevision,
        CancellationToken cancellationToken) =>
        dbContext.TenantTerminationTerminalReceipts.SingleOrDefaultAsync(
            receipt =>
                receipt.ProcessId == processId &&
                receipt.VerificationOperationRevision ==
                    verificationOperationRevision,
            cancellationToken);
}

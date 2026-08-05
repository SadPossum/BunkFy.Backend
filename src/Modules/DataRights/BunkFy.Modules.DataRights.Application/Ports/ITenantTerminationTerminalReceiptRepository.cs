namespace BunkFy.Modules.DataRights.Application.Ports;

using BunkFy.Modules.DataRights.Domain.Aggregates;

public interface ITenantTerminationTerminalReceiptRepository
{
    Task AddAsync(
        TenantTerminationTerminalReceipt receipt,
        CancellationToken cancellationToken);

    Task<TenantTerminationTerminalReceipt?> GetAsync(
        Guid receiptId,
        CancellationToken cancellationToken);

    Task<TenantTerminationTerminalReceipt?> GetByIdempotencyKeyAsync(
        Guid idempotencyKey,
        CancellationToken cancellationToken);

    Task<TenantTerminationTerminalReceipt?> GetByProcessAsync(
        Guid processId,
        long verificationOperationRevision,
        CancellationToken cancellationToken);
}

namespace BunkFy.Modules.Ingestion.Persistence.Repositories;

using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Domain.Receipts;
using Microsoft.EntityFrameworkCore;

internal sealed class ObservationReceiptRepository(IngestionDbContext dbContext) : IObservationReceiptRepository
{
    public Task<ObservationReceipt?> GetAsync(Guid receiptId, CancellationToken cancellationToken) =>
        dbContext.ObservationReceipts.FirstOrDefaultAsync(receipt => receipt.Id == receiptId, cancellationToken);

    public Task<ObservationReceipt?> FindByOperationAsync(
        Guid connectionId,
        Guid operationId,
        CancellationToken cancellationToken) =>
        dbContext.ObservationReceipts.FirstOrDefaultAsync(
            receipt => receipt.ConnectionId == connectionId && receipt.OperationId == operationId,
            cancellationToken);

    public Task<ObservationReceipt?> FindByDeduplicationKeyAsync(
        Guid connectionId,
        string deduplicationKey,
        CancellationToken cancellationToken) =>
        dbContext.ObservationReceipts.FirstOrDefaultAsync(
            receipt => receipt.ConnectionId == connectionId && receipt.DeduplicationKey == deduplicationKey,
            cancellationToken);

    public Task AddAsync(ObservationReceipt receipt, CancellationToken cancellationToken)
    {
        dbContext.ObservationReceipts.Add(receipt);
        return Task.CompletedTask;
    }
}

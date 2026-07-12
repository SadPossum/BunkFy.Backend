namespace BunkFy.Modules.Ingestion.Application.Ports;

using BunkFy.Modules.Ingestion.Domain.Receipts;

public interface IObservationReceiptRepository
{
    Task<ObservationReceipt?> GetAsync(Guid receiptId, CancellationToken cancellationToken);

    Task<ObservationReceipt?> FindByOperationAsync(
        Guid connectionId,
        Guid operationId,
        CancellationToken cancellationToken);

    Task<ObservationReceipt?> FindByDeduplicationKeyAsync(
        Guid connectionId,
        string deduplicationKey,
        CancellationToken cancellationToken);

    Task AddAsync(ObservationReceipt receipt, CancellationToken cancellationToken);
}

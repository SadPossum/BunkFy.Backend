namespace BunkFy.Modules.Ingestion.Application.Ports;

using Gma.Framework.Pagination;
using BunkFy.Modules.Ingestion.Contracts;

public interface IIngestionOperationsReader
{
    Task<AdapterConnectionDto?> GetConnectionAsync(Guid propertyId, Guid connectionId, CancellationToken cancellationToken);
    Task<AdapterConnectionHealthDto?> GetConnectionHealthAsync(
        Guid propertyId,
        Guid connectionId,
        DateTimeOffset evaluatedAtUtc,
        CancellationToken cancellationToken);
    Task<AdapterConnectionListResponse> ListConnectionsAsync(
        Guid propertyId,
        AdapterConnectionStatus? status,
        PageRequest pageRequest,
        CancellationToken cancellationToken);
    Task<IngestionRunDto?> GetRunAsync(Guid propertyId, Guid runId, CancellationToken cancellationToken);
    Task<IngestionRunListResponse> ListRunsAsync(
        Guid propertyId,
        Guid? connectionId,
        IngestionRunStatus? status,
        PageRequest pageRequest,
        CancellationToken cancellationToken);
    Task<ObservationReceiptDto?> GetReceiptAsync(Guid propertyId, Guid receiptId, CancellationToken cancellationToken);
    Task<ObservationReceiptListResponse> ListReceiptsAsync(
        Guid propertyId,
        Guid? connectionId,
        Guid? runId,
        ObservationReceiptStatus? status,
        PageRequest pageRequest,
        CancellationToken cancellationToken);
    Task<ObservationReprocessingAttemptDto?> GetReprocessingAttemptAsync(
        Guid propertyId,
        Guid attemptId,
        CancellationToken cancellationToken);
    Task<IReadOnlyCollection<ObservationReprocessingOutputDto>> ListReprocessingOutputsAsync(
        Guid attemptId,
        CancellationToken cancellationToken);
    Task<ObservationReprocessingAttemptListResponse> ListReprocessingAttemptsAsync(
        Guid propertyId,
        Guid? sourceReceiptId,
        ObservationReprocessingStatus? status,
        PageRequest pageRequest,
        CancellationToken cancellationToken);
}

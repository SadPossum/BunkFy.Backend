namespace BunkFy.Modules.Ingestion.Application.Ports;

internal sealed record IngestionSourceGraphCoordinate(
    Guid RecordId,
    Guid ConnectionId,
    Guid SourceLinkId);

internal interface IIngestionSourceGraphLocator
{
    Task<IngestionSourceGraphCoordinate?> FindReceiptAsync(
        Guid receiptId,
        CancellationToken cancellationToken);

    Task<IngestionSourceGraphCoordinate?> FindProposalAsync(
        Guid proposalId,
        CancellationToken cancellationToken);

    Task<IngestionSourceGraphCoordinate?> FindDispatchAsync(
        Guid dispatchId,
        CancellationToken cancellationToken);

    Task<IngestionSourceGraphCoordinate?> FindReprocessingAttemptAsync(
        Guid attemptId,
        CancellationToken cancellationToken);

    Task<IngestionSourceGraphCoordinate?> FindSourceLinkAsync(
        Guid sourceLinkId,
        CancellationToken cancellationToken);

    Task<IngestionSourceGraphCoordinate?> FindAcceptedCancellationAsync(
        Guid reservationId,
        CancellationToken cancellationToken);
}

namespace BunkFy.Modules.Ingestion.Application.Ports;

using BunkFy.Modules.Ingestion.Domain.DataRights;
using BunkFy.Modules.Ingestion.Domain.Proposals;
using BunkFy.Modules.Ingestion.Domain.Receipts;
using BunkFy.Modules.Ingestion.Domain.Reprocessing;
using BunkFy.Modules.Ingestion.Domain.Reservations;

internal interface IIngestionAnonymisationRestoreRepository
{
    Task<IngestionAnonymisationTombstone?> GetTombstoneAsync(
        Guid sourceLinkId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<IngestionAnonymisationRecordPlanEntry>>
        GetPlanAsync(
            Guid tombstoneId,
            CancellationToken cancellationToken);

    Task<int> CountFingerprintsAsync(
        Guid tombstoneId,
        CancellationToken cancellationToken);

    Task<IngestionAnonymisationRestoreGraphLoadResult>
        LoadInitialGraphAsync(
            Guid propertyId,
            Guid sourceLinkId,
            CancellationToken cancellationToken);

    Task<IngestionAnonymisationRestoreGraphLoadResult>
        LoadPlannedGraphAsync(
            Guid propertyId,
            Guid sourceLinkId,
            IReadOnlyCollection<IngestionAnonymisationRecordPlanEntry> plan,
            CancellationToken cancellationToken);

    void AddRestoreState(
        IngestionAnonymisationTombstone tombstone,
        IReadOnlyCollection<IngestionAnonymisationFingerprint> fingerprints,
        IReadOnlyCollection<IngestionAnonymisationRecordPlanEntry> plan);
}

internal sealed record IngestionAnonymisationRestoreGraph(
    ReservationSourceLink SourceLink,
    IReadOnlyCollection<ChangeProposal> Proposals,
    IReadOnlyCollection<ReservationDispatch> Dispatches,
    IReadOnlyCollection<ObservationReceipt> Receipts,
    IReadOnlyCollection<ObservationReprocessingAttempt> Attempts,
    IReadOnlyCollection<ObservationReprocessingOutput> Outputs)
{
    public int RecordCount =>
        checked(
            1 +
            this.Proposals.Count +
            this.Dispatches.Count +
            this.Receipts.Count +
            this.Attempts.Count +
            this.Outputs.Count);
}

internal sealed record IngestionAnonymisationRestoreGraphLoadResult(
    IngestionAnonymisationRestoreGraphLoadStatus Status,
    IngestionAnonymisationRestoreGraph? Graph)
{
    public static IngestionAnonymisationRestoreGraphLoadResult Found(
        IngestionAnonymisationRestoreGraph graph) =>
        new(IngestionAnonymisationRestoreGraphLoadStatus.Found, graph);

    public static IngestionAnonymisationRestoreGraphLoadResult NotFound() =>
        new(IngestionAnonymisationRestoreGraphLoadStatus.NotFound, Graph: null);

    public static IngestionAnonymisationRestoreGraphLoadResult Unavailable() =>
        new(
            IngestionAnonymisationRestoreGraphLoadStatus.Unavailable,
            Graph: null);

    public static IngestionAnonymisationRestoreGraphLoadResult TooLarge() =>
        new(
            IngestionAnonymisationRestoreGraphLoadStatus.TooLarge,
            Graph: null);
}

internal enum IngestionAnonymisationRestoreGraphLoadStatus
{
    Found = 1,
    NotFound = 2,
    Unavailable = 3,
    TooLarge = 4
}

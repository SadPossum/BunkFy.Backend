namespace BunkFy.Modules.Ingestion.Application.Ports;

using BunkFy.Modules.Ingestion.Domain.Connections;
using BunkFy.Modules.Ingestion.Domain.Proposals;
using BunkFy.Modules.Ingestion.Domain.Receipts;
using BunkFy.Modules.Ingestion.Domain.Reprocessing;
using BunkFy.Modules.Ingestion.Domain.Reservations;
using BunkFy.Modules.Properties.Contracts;

internal interface IIngestionAnonymisationEligibilityRepository
{
    Task<IngestionAnonymisationEligibilityLoadResult> LoadAsync(
        Guid propertyId,
        Guid sourceLinkId,
        CancellationToken cancellationToken);
}

internal sealed record IngestionAnonymisationEligibilityLoadResult(
    IngestionAnonymisationEligibilityLoadStatus Status,
    IngestionAnonymisationEligibilitySnapshot? Snapshot)
{
    public static IngestionAnonymisationEligibilityLoadResult Found(
        IngestionAnonymisationEligibilitySnapshot snapshot) =>
        new(IngestionAnonymisationEligibilityLoadStatus.Found, snapshot);

    public static IngestionAnonymisationEligibilityLoadResult NotFound() =>
        new(IngestionAnonymisationEligibilityLoadStatus.NotFound, Snapshot: null);

    public static IngestionAnonymisationEligibilityLoadResult TooLarge() =>
        new(IngestionAnonymisationEligibilityLoadStatus.TooLarge, Snapshot: null);

    public static IngestionAnonymisationEligibilityLoadResult Unavailable() =>
        new(IngestionAnonymisationEligibilityLoadStatus.Unavailable, Snapshot: null);
}

internal enum IngestionAnonymisationEligibilityLoadStatus
{
    Found = 1,
    NotFound = 2,
    TooLarge = 3,
    Unavailable = 4
}

internal sealed record IngestionAnonymisationEligibilitySnapshot(
    IngestionAnonymisationSourceLinkSnapshot SourceLink,
    IngestionAnonymisationConnectionSnapshot Connection,
    IngestionAnonymisationPropertySnapshot? Property,
    int ActiveLegalHoldCount,
    IReadOnlyCollection<IngestionAnonymisationReceiptSnapshot> Receipts,
    IReadOnlyCollection<IngestionAnonymisationProposalSnapshot> Proposals,
    IReadOnlyCollection<IngestionAnonymisationDispatchSnapshot> Dispatches,
    IReadOnlyCollection<IngestionAnonymisationAttemptSnapshot> Attempts,
    IReadOnlyCollection<IngestionAnonymisationOutputSnapshot> Outputs)
{
    public int GraphRecordCount =>
        checked(
            1 +
            this.Receipts.Count +
            this.Proposals.Count +
            this.Dispatches.Count +
            this.Attempts.Count +
            this.Outputs.Count);
}

internal sealed record IngestionAnonymisationSourceLinkSnapshot(
    Guid Id,
    Guid PropertyId,
    Guid ConnectionId,
    Guid? ReservationId,
    ReservationSourceLinkState State,
    Guid LastObservedReceiptId,
    Guid? LastAppliedReceiptId,
    Guid? ActiveProductOperationId,
    Guid? DeferredReceiptId,
    long Version);

internal sealed record IngestionAnonymisationConnectionSnapshot(
    AdapterConnectionState State,
    long Version);

internal sealed record IngestionAnonymisationPropertySnapshot(
    bool IsKnown,
    bool IsActive,
    PropertyProcessingStatus ProcessingStatus,
    long TopologySourceVersion,
    long PolicySourceVersion,
    long RetentionFenceVersion,
    PropertyGovernancePolicyBinding? GovernancePolicy);

internal sealed record IngestionAnonymisationReceiptSnapshot(
    Guid Id,
    ObservationReceiptState State,
    RawPayloadRetentionState RawPayloadRetentionState,
    long RawPayloadVersion,
    Guid? ActiveReprocessingAttemptId,
    DateTimeOffset? ReprocessingReservationExpiresAtUtc,
    Guid? SourceReceiptId,
    Guid? ReprocessingAttemptId);

internal sealed record IngestionAnonymisationProposalSnapshot(
    Guid Id,
    Guid ReceiptId,
    ChangeProposalState State,
    long Version);

internal sealed record IngestionAnonymisationDispatchSnapshot(
    Guid Id,
    Guid ReceiptId,
    ReservationDispatchState State,
    long Version);

internal sealed record IngestionAnonymisationAttemptSnapshot(
    Guid Id,
    Guid SourceReceiptId,
    ObservationReprocessingState State,
    long Version);

internal sealed record IngestionAnonymisationOutputSnapshot(
    Guid Id,
    Guid AttemptId,
    int OutputIndex,
    ObservationReprocessingOutputDisposition Disposition,
    Guid? ReceiptId);

namespace BunkFy.Modules.Ingestion.Persistence.Repositories;

using BunkFy.Modules.Ingestion.Domain.Proposals;
using BunkFy.Modules.Ingestion.Domain.Receipts;
using BunkFy.Modules.Ingestion.Domain.Reprocessing;
using BunkFy.Modules.Ingestion.Domain.Reservations;

internal sealed record ReservationSourceLinkDataRightsExport(
    Guid SourceLinkId,
    Guid PropertyId,
    Guid ConnectionId,
    string SourceSystem,
    string SourceReference,
    Guid? ReservationId,
    ReservationSourceLinkState State,
    Guid LastObservedReceiptId,
    string? LastObservedSourceRevision,
    long? LastObservedSourceSequence,
    DateTimeOffset? LastObservedSourceUpdatedAtUtc,
    string LastObservedContentHash,
    Guid? LastAppliedReceiptId,
    string? LastAppliedSourceRevision,
    long? LastAppliedSourceSequence,
    long? LastAppliedReservationDetailsRevision,
    SensitiveHistoryReferenceDataRightsExport?
        LastAppliedOperationalBaseline,
    Guid? LastProductOperationId,
    Guid? ActiveProductOperationId,
    Guid? DeferredReceiptId,
    long Version,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc);

internal sealed record ObservationReceiptDataRightsExport(
    Guid ReceiptId,
    Guid PropertyId,
    Guid ConnectionId,
    Guid? RunId,
    Guid OperationId,
    string SourceRecordType,
    string ExternalId,
    string? SourceRevision,
    string DeduplicationKey,
    string ContentHash,
    string? OperatingCountryCode,
    string? PolicyId,
    int? PolicyVersion,
    string? DataRegionId,
    string? TransferProfileId,
    string? RetentionPolicyId,
    int? RetentionPolicyVersion,
    string? PolicyContentSha256,
    string? PurposeCode,
    string? ProcessingSurface,
    string? SourceProvenance,
    DateTimeOffset? PolicyEffectiveAtUtc,
    DateTimeOffset? PolicyExpiresAtUtc,
    DateTimeOffset? PolicyEvaluatedAtUtc,
    Guid RawPayloadFileId,
    RawPayloadRetentionState RawPayloadRetentionState,
    DateTimeOffset RawPayloadRetainUntilUtc,
    DateTimeOffset? RawPayloadPurgeStartedAtUtc,
    DateTimeOffset? RawPayloadPurgedAtUtc,
    long RawPayloadVersion,
    Guid? ActiveReprocessingAttemptId,
    DateTimeOffset? ReprocessingReservationExpiresAtUtc,
    Guid? SourceReceiptId,
    Guid? ReprocessingAttemptId,
    string? ParserType,
    int? ParserVersion,
    int? ParserOutputIndex,
    DateTimeOffset? SourceUpdatedAtUtc,
    DateTimeOffset ObservedAtUtc,
    ObservationReceiptState State,
    string? RejectionReason,
    DateTimeOffset ReceivedAtUtc,
    DateTimeOffset? ProcessedAtUtc);

internal sealed record ChangeProposalDataRightsExport(
    Guid ProposalId,
    Guid PropertyId,
    Guid ConnectionId,
    Guid ReceiptId,
    Guid ReservationId,
    Guid SourcePayloadFileId,
    long BaseReservationDetailsRevision,
    string ReasonCode,
    SensitiveHistoryReferenceDataRightsExport? Diff,
    ChangeProposalState State,
    string? DecisionReason,
    Guid? ProductOperationId,
    long Version,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? DecidedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    DateTimeOffset? SensitiveDataRetainUntilUtc,
    DateTimeOffset? SensitiveDataRedactedAtUtc);

internal sealed record ReservationDispatchDataRightsExport(
    Guid DispatchId,
    Guid SourceLinkId,
    ReservationDispatchTriggerKind TriggerKind,
    Guid TriggerId,
    Guid ReceiptId,
    Guid ConnectionId,
    Guid PropertyId,
    Guid? ReservationId,
    ReservationDispatchKind Kind,
    string? SourceRevision,
    long? SourceSequence,
    SensitiveHistoryReferenceDataRightsExport? NormalizedSnapshot,
    long? ExpectedDetailsRevision,
    ReservationDispatchState State,
    long? ResultDetailsRevision,
    long? ResultReservationVersion,
    string? ErrorCode,
    long Version,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    DateTimeOffset? SensitiveDataRetainUntilUtc,
    DateTimeOffset? SensitiveDataRedactedAtUtc);

internal sealed record ObservationReprocessingAttemptDataRightsExport(
    Guid AttemptId,
    Guid PropertyId,
    Guid ConnectionId,
    Guid SourceReceiptId,
    Guid TaskRunId,
    string ParserType,
    int ParserVersion,
    ObservationReprocessingState State,
    int LastTaskAttempt,
    int ParsedCount,
    int AcceptedCount,
    int DuplicateCount,
    int RejectedCount,
    string? LastErrorCode,
    DateTimeOffset RequestedAtUtc,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    DateTimeOffset ReservationExpiresAtUtc,
    long Version);

internal sealed record ObservationReprocessingOutputDataRightsExport(
    Guid OutputId,
    Guid AttemptId,
    int OutputIndex,
    Guid OperationId,
    Guid? ReceiptId,
    ObservationReprocessingOutputDisposition Disposition,
    string RecordType,
    string ExternalId,
    string? SourceRevision,
    string ContentHash,
    string? ErrorCode,
    DateTimeOffset RecordedAtUtc);

internal sealed record RawPayloadChunkDataRightsExport(
    Guid ReceiptId,
    Guid RawPayloadFileId,
    int ChunkIndex,
    int ChunkCount,
    int TotalBytes,
    string ContentType,
    string ContentSha256,
    byte[] Content);

internal sealed record SensitiveHistoryReferenceDataRightsExport(
    string ContentKind,
    string ContentSha256,
    int TotalBytes,
    int ChunkCount,
    string Encoding);

internal sealed record SensitiveHistoryChunkDataRightsExport(
    SensitiveHistoryChunkMetadataDataRightsExport Metadata,
    byte[] Content);

internal sealed record SensitiveHistoryChunkMetadataDataRightsExport(
    string ParentRecordType,
    Guid ParentRecordId,
    string ContentKind,
    int ChunkIndex,
    int ChunkCount,
    int TotalBytes,
    string ContentSha256,
    string Encoding);

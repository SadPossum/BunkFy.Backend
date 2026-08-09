namespace BunkFy.Modules.Ingestion.Persistence.Repositories;

using BunkFy.Adapter.Abstractions;
using BunkFy.Modules.Ingestion.Domain.Connections;
using BunkFy.Modules.Ingestion.Domain.Controls;
using BunkFy.Modules.Ingestion.Domain.Credentials;
using BunkFy.Modules.Ingestion.Domain.DataRights;
using BunkFy.Modules.Ingestion.Domain.LegalHolds;
using BunkFy.Modules.Ingestion.Domain.Proposals;
using BunkFy.Modules.Ingestion.Domain.Receipts;
using BunkFy.Modules.Ingestion.Domain.Reprocessing;
using BunkFy.Modules.Ingestion.Domain.Reservations;
using BunkFy.Modules.Ingestion.Domain.Retention;
using BunkFy.Modules.Ingestion.Domain.Runs;

[AttributeUsage(AttributeTargets.Property)]
internal sealed class IngestionTenantExportFieldAttribute(string fieldId)
    : Attribute
{
    public string FieldId { get; } = fieldId;
}

internal sealed record IngestionAdapterConnectionTenantExport(
    [property: IngestionTenantExportField("ingestion.operations.scope-id")]
    string ScopeId,
    [property: IngestionTenantExportField("ingestion.operations.property-id")]
    Guid PropertyId,
    [property: IngestionTenantExportField("ingestion.operations.connection-id")]
    Guid ConnectionId,
    [property: IngestionTenantExportField("ingestion.tenant.adapter-connection")]
    IngestionAdapterConnectionStateTenantExport Connection);

internal sealed record IngestionAdapterConnectionStateTenantExport(
    string AdapterType,
    AdapterExecutionMode ExecutionMode,
    IngestionConflictPolicy ConflictPolicy,
    string ConfigurationReference,
    string? Checkpoint,
    int? PollingIntervalSeconds,
    int? PollingScheduleMaxAttempts,
    DateTimeOffset? PollingScheduleConfiguredAtUtc,
    Guid? RemoteLeaseRunId,
    Guid? RemoteLeaseId,
    Guid? RemoteLeaseClaimId,
    Guid? RemoteLeaseCredentialId,
    Guid? RemoteLeaseWorkerId,
    long RemoteLeaseEpoch,
    DateTimeOffset? RemoteLeaseExpiresAtUtc,
    AdapterConnectionState State,
    long Version,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc);

internal sealed record IngestionConnectionManagementOperationTenantExport(
    [property: IngestionTenantExportField("ingestion.operations.scope-id")]
    string ScopeId,
    [property: IngestionTenantExportField("ingestion.operations.property-id")]
    Guid PropertyId,
    [property: IngestionTenantExportField("ingestion.operations.connection-id")]
    Guid ConnectionId,
    [property: IngestionTenantExportField("ingestion.operations.operation-id")]
    Guid OperationId,
    [property: IngestionTenantExportField(
        "ingestion.tenant.connection-management-operation")]
    IngestionConnectionManagementOperationStateTenantExport Operation);

internal sealed record IngestionConnectionManagementOperationStateTenantExport(
    int Kind,
    long ExpectedVersion,
    long ResultVersion,
    DateTimeOffset CompletedAtUtc);

internal sealed record IngestionAdapterCredentialTenantExport(
    [property: IngestionTenantExportField("ingestion.operations.scope-id")]
    string ScopeId,
    [property: IngestionTenantExportField("ingestion.operations.connection-id")]
    Guid ConnectionId,
    [property: IngestionTenantExportField("ingestion.operations.id")]
    Guid CredentialId,
    [property: IngestionTenantExportField(
        "ingestion.tenant.adapter-credential-metadata")]
    IngestionAdapterCredentialMetadataTenantExport CredentialMetadata);

internal sealed record IngestionAdapterCredentialMetadataTenantExport(
    string AdapterType,
    int AdapterProtocolVersion,
    int ConfigurationSchemaVersion,
    string SourceSystem,
    int Slot,
    string Label,
    AdapterIngressCredentialState State,
    DateTimeOffset ExpiresAtUtc,
    string CreatedBy,
    DateTimeOffset CreatedAtUtc,
    string? RevokedBy,
    DateTimeOffset? RevokedAtUtc,
    DateTimeOffset? LastAuthenticatedAtUtc,
    long Version);

internal sealed record IngestionAdapterIngressControlTenantExport(
    [property: IngestionTenantExportField("ingestion.operations.scope-id")]
    string ScopeId,
    [property: IngestionTenantExportField(
        "ingestion.tenant.adapter-ingress-control")]
    IngestionAdapterIngressControlStateTenantExport IngressControl);

internal sealed record IngestionAdapterIngressControlStateTenantExport(
    bool IsSuspended,
    string LastReasonCode,
    string LastChangedBy,
    DateTimeOffset LastChangedAtUtc,
    DateTimeOffset? SuspendedAtUtc,
    DateTimeOffset? ResumedAtUtc,
    long Version);

internal sealed record IngestionRunTenantExport(
    [property: IngestionTenantExportField("ingestion.operations.scope-id")]
    string ScopeId,
    [property: IngestionTenantExportField("ingestion.operations.property-id")]
    Guid PropertyId,
    [property: IngestionTenantExportField("ingestion.operations.connection-id")]
    Guid ConnectionId,
    [property: IngestionTenantExportField("ingestion.operations.id")]
    Guid RunId,
    [property: IngestionTenantExportField("ingestion.tenant.run")]
    IngestionRunStateTenantExport Run);

internal sealed record IngestionRunStateTenantExport(
    IngestionRunExecutionKind ExecutionKind,
    Guid? TaskRunId,
    int? TaskAttempt,
    Guid? RemoteLeaseId,
    Guid? RemoteClaimId,
    long? RemoteLeaseEpoch,
    Guid? RemoteCredentialId,
    Guid? RemoteWorkerId,
    DateTimeOffset? RemoteLeaseExpiresAtUtc,
    string? StartingCheckpoint,
    string? AcceptedCheckpoint,
    IngestionRunState State,
    int ObservedCount,
    int AcceptedCount,
    int RejectedCount,
    string? ErrorCode,
    long Version,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? CompletedAtUtc);

internal sealed record IngestionObservationReceiptTenantExport(
    [property: IngestionTenantExportField("ingestion.operations.scope-id")]
    string ScopeId,
    [property: IngestionTenantExportField("ingestion.operations.property-id")]
    Guid PropertyId,
    [property: IngestionTenantExportField("ingestion.operations.connection-id")]
    Guid ConnectionId,
    [property: IngestionTenantExportField("ingestion.operations.id")]
    Guid ReceiptId,
    [property: IngestionTenantExportField(
        "ingestion.tenant.observation-evidence")]
    IngestionObservationEvidenceTenantExport ObservationEvidence);

internal sealed record IngestionObservationEvidenceTenantExport(
    Guid? RunId,
    Guid OperationId,
    string SourceRecordType,
    string ExternalId,
    string? SourceRevision,
    string DeduplicationKey,
    string ContentHash,
    IngestionAdapterProvenanceTenantExport? AdapterProvenance,
    IngestionCountryPolicyEvidenceTenantExport? CountryPolicyEvidence,
    Guid RawPayloadFileId,
    RawPayloadRetentionState RawPayloadRetentionState,
    DateTimeOffset RawPayloadRetainUntilUtc,
    Guid? RawPayloadPurgeClaimId,
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
    DateTimeOffset? ProcessedAtUtc,
    DateTimeOffset? AnonymisedAtUtc);

internal sealed record IngestionAdapterProvenanceTenantExport(
    Guid? CredentialId,
    string AdapterType,
    int AdapterProtocolVersion,
    int ConfigurationSchemaVersion,
    string SourceSystem,
    string? CustomerOwner);

internal sealed record IngestionCountryPolicyEvidenceTenantExport(
    string OperatingCountryCode,
    string PolicyId,
    int PolicyVersion,
    string DataRegionId,
    string TransferProfileId,
    string RetentionPolicyId,
    int RetentionPolicyVersion,
    string ContentSha256,
    string PurposeCode,
    string ProcessingSurface,
    string SourceProvenance,
    DateTimeOffset PolicyEffectiveAtUtc,
    DateTimeOffset PolicyExpiresAtUtc,
    DateTimeOffset EvaluatedAtUtc);

internal sealed record IngestionReprocessingAttemptTenantExport(
    [property: IngestionTenantExportField("ingestion.operations.scope-id")]
    string ScopeId,
    [property: IngestionTenantExportField("ingestion.operations.property-id")]
    Guid PropertyId,
    [property: IngestionTenantExportField("ingestion.operations.connection-id")]
    Guid ConnectionId,
    [property: IngestionTenantExportField("ingestion.operations.id")]
    Guid AttemptId,
    [property: IngestionTenantExportField(
        "ingestion.tenant.reprocessing-attempt")]
    IngestionReprocessingAttemptStateTenantExport ReprocessingAttempt);

internal sealed record IngestionReprocessingAttemptStateTenantExport(
    Guid SourceReceiptId,
    Guid TaskRunId,
    string ParserType,
    int ParserVersion,
    string RequestedBy,
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

internal sealed record IngestionReprocessingOutputTenantExport(
    [property: IngestionTenantExportField("ingestion.operations.scope-id")]
    string ScopeId,
    [property: IngestionTenantExportField("ingestion.operations.id")]
    Guid OutputId,
    [property: IngestionTenantExportField(
        "ingestion.tenant.reprocessing-output")]
    IngestionReprocessingOutputStateTenantExport ReprocessingOutput);

internal sealed record IngestionReprocessingOutputStateTenantExport(
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
    DateTimeOffset RecordedAtUtc,
    long Version,
    DateTimeOffset? AnonymisedAtUtc);

internal sealed record IngestionChangeProposalTenantExport(
    [property: IngestionTenantExportField("ingestion.operations.scope-id")]
    string ScopeId,
    [property: IngestionTenantExportField("ingestion.operations.property-id")]
    Guid PropertyId,
    [property: IngestionTenantExportField("ingestion.operations.connection-id")]
    Guid ConnectionId,
    [property: IngestionTenantExportField("ingestion.operations.id")]
    Guid ProposalId,
    [property: IngestionTenantExportField(
        "ingestion.tenant.change-proposal")]
    IngestionChangeProposalStateTenantExport ChangeProposal);

internal sealed record IngestionChangeProposalStateTenantExport(
    Guid ReceiptId,
    Guid ReservationId,
    Guid SourcePayloadFileId,
    long BaseReservationDetailsRevision,
    string ReasonCode,
    IngestionLargeTextReferenceTenantExport? Diff,
    ChangeProposalState State,
    string? DecisionActor,
    string? DecisionReason,
    Guid? ProductOperationId,
    long Version,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? DecidedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    DateTimeOffset? SensitiveDataRetainUntilUtc,
    DateTimeOffset? SensitiveDataRedactedAtUtc,
    DateTimeOffset? AnonymisedAtUtc);

internal sealed record IngestionReservationSourceLinkTenantExport(
    [property: IngestionTenantExportField("ingestion.operations.scope-id")]
    string ScopeId,
    [property: IngestionTenantExportField("ingestion.operations.property-id")]
    Guid PropertyId,
    [property: IngestionTenantExportField("ingestion.operations.connection-id")]
    Guid ConnectionId,
    [property: IngestionTenantExportField("ingestion.operations.id")]
    Guid SourceLinkId,
    [property: IngestionTenantExportField(
        "ingestion.tenant.reservation-source-link")]
    IngestionReservationSourceLinkStateTenantExport SourceLink);

internal sealed record IngestionReservationSourceLinkStateTenantExport(
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
    IngestionLargeTextReferenceTenantExport? LastAppliedOperationalBaseline,
    Guid? LastProductOperationId,
    Guid? ActiveProductOperationId,
    Guid? DeferredReceiptId,
    long Version,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc,
    DateTimeOffset? AnonymisedAtUtc);

internal sealed record IngestionReservationDispatchTenantExport(
    [property: IngestionTenantExportField("ingestion.operations.scope-id")]
    string ScopeId,
    [property: IngestionTenantExportField("ingestion.operations.property-id")]
    Guid PropertyId,
    [property: IngestionTenantExportField("ingestion.operations.connection-id")]
    Guid ConnectionId,
    [property: IngestionTenantExportField("ingestion.operations.id")]
    Guid DispatchId,
    [property: IngestionTenantExportField(
        "ingestion.tenant.reservation-dispatch")]
    IngestionReservationDispatchStateTenantExport Dispatch);

internal sealed record IngestionReservationDispatchStateTenantExport(
    Guid SourceLinkId,
    ReservationDispatchTriggerKind TriggerKind,
    Guid TriggerId,
    Guid ReceiptId,
    Guid? ReservationId,
    ReservationDispatchKind Kind,
    string? SourceRevision,
    long? SourceSequence,
    IngestionLargeTextReferenceTenantExport? NormalizedSnapshot,
    long? ExpectedDetailsRevision,
    ReservationDispatchState State,
    long? ResultDetailsRevision,
    long? ResultReservationVersion,
    string? ErrorCode,
    long Version,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    DateTimeOffset? SensitiveDataRetainUntilUtc,
    DateTimeOffset? SensitiveDataRedactedAtUtc,
    DateTimeOffset? AnonymisedAtUtc);

internal sealed record IngestionLegalHoldTenantExport(
    [property: IngestionTenantExportField("ingestion.operations.scope-id")]
    string ScopeId,
    [property: IngestionTenantExportField("ingestion.operations.property-id")]
    Guid PropertyId,
    [property: IngestionTenantExportField("ingestion.operations.id")]
    Guid HoldId,
    [property: IngestionTenantExportField("ingestion.tenant.legal-hold")]
    IngestionLegalHoldStateTenantExport LegalHold);

internal sealed record IngestionLegalHoldStateTenantExport(
    string Reason,
    LegalHoldState State,
    string PlacedBy,
    DateTimeOffset PlacedAtUtc,
    string? ReleasedBy,
    string? ReleaseReason,
    DateTimeOffset? ReleasedAtUtc,
    long Version);

internal sealed record IngestionRetentionExecutionTenantExport(
    [property: IngestionTenantExportField("ingestion.operations.scope-id")]
    string ScopeId,
    [property: IngestionTenantExportField("ingestion.operations.id")]
    Guid ExecutionId,
    [property: IngestionTenantExportField(
        "ingestion.tenant.retention-execution")]
    IngestionRetentionExecutionStateTenantExport RetentionExecution);

internal sealed record IngestionRetentionExecutionStateTenantExport(
    string DataClassKey,
    int ExecutionPolicyVersion,
    int Attempt,
    IngestionRetentionExecutionState State,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset DeadlineUtc,
    DateTimeOffset? CompletedAtUtc,
    int AffectedCount,
    int? RemainingCount,
    string? OutcomeCode,
    DateTimeOffset? HoldReviewDueAtUtc,
    long Version);

internal sealed record IngestionAnonymisationReceiptTenantExport(
    [property: IngestionTenantExportField("ingestion.operations.scope-id")]
    string ScopeId,
    [property: IngestionTenantExportField("ingestion.operations.property-id")]
    Guid PropertyId,
    [property: IngestionTenantExportField("ingestion.operations.id")]
    Guid ReceiptId,
    [property: IngestionTenantExportField(
        "ingestion.tenant.anonymisation-proof")]
    IngestionAnonymisationReceiptProofTenantExport AnonymisationProof);

internal sealed record IngestionAnonymisationReceiptProofTenantExport(
    int ContractVersion,
    Guid WorkItemId,
    Guid IdempotencyKey,
    Guid CaseId,
    long ApprovalRevision,
    long OperationRevision,
    Guid SourceLinkId,
    long SelectedSourceLinkVersion,
    long ResultingSourceLinkVersion,
    IngestionAnonymisationDisposition Disposition,
    IngestionAnonymisationReason Reason,
    int GraphRecordCount,
    int FingerprintCount,
    int RawPayloadCount,
    string ApprovalEvidenceSha256,
    string PolicyEvidenceSha256,
    string OperationFenceSha256,
    string ActorId,
    DateTimeOffset CompletedAtUtc,
    string CanonicalSha256);

internal sealed record IngestionAnonymisationTombstoneTenantExport(
    [property: IngestionTenantExportField("ingestion.operations.scope-id")]
    string ScopeId,
    [property: IngestionTenantExportField("ingestion.operations.property-id")]
    Guid PropertyId,
    [property: IngestionTenantExportField("ingestion.operations.connection-id")]
    Guid ConnectionId,
    [property: IngestionTenantExportField("ingestion.operations.id")]
    Guid SourceLinkId,
    [property: IngestionTenantExportField(
        "ingestion.tenant.anonymisation-proof")]
    IngestionAnonymisationTombstoneProofTenantExport AnonymisationProof);

internal sealed record IngestionAnonymisationTombstoneProofTenantExport(
    int ContractVersion,
    long Revision,
    IngestionAnonymisationTombstoneState State,
    IngestionAnonymisationOrigin Origin,
    long SelectedSourceLinkVersion,
    long ResultingSourceLinkVersion,
    Guid WorkItemId,
    Guid IdempotencyKey,
    Guid CaseId,
    long ApprovalRevision,
    long OperationRevision,
    string ApprovalEvidenceSha256,
    string PolicyEvidenceSha256,
    string OperationFenceSha256,
    string ActorId,
    DateTimeOffset ExecutionStartedAtUtc,
    int OwnerReceiptContractVersion,
    Guid OwnerReceiptId,
    string OwnerReceiptSha256,
    DateTimeOffset OriginallyCompletedAtUtc,
    Guid LedgerEntryId,
    long TenantSequence,
    string LedgerEntrySha256,
    int GraphRecordCount,
    int FingerprintCount,
    int RawPayloadCount,
    DateTimeOffset ReplayStartedAtUtc,
    DateTimeOffset? LastReplayedAtUtc);

internal sealed record IngestionLargeTextReferenceTenantExport(
    string ContentKind,
    string ContentSha256,
    int TotalBytes,
    int ChunkCount,
    string Encoding);

internal sealed record IngestionLargeTextChunkTenantExport(
    [property: IngestionTenantExportField("ingestion.operations.scope-id")]
    string ScopeId,
    [property: IngestionTenantExportField("ingestion.operations.id")]
    Guid ParentRecordId,
    [property: IngestionTenantExportField(
        "ingestion.tenant.large-text-metadata")]
    IngestionLargeTextChunkMetadataTenantExport LargeTextMetadata,
    [property: IngestionTenantExportField(
        "ingestion.tenant.large-text-content")]
    byte[] Content);

internal sealed record IngestionLargeTextChunkMetadataTenantExport(
    string ParentRecordType,
    string ContentKind,
    int ChunkIndex,
    int ChunkCount,
    int TotalBytes,
    string ContentSha256,
    string Encoding);

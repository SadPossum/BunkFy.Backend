namespace BunkFy.Modules.Ingestion.Domain.Receipts;

using BunkFy.Adapter.Abstractions;
using Gma.Framework.Domain.Models;
using Gma.Framework.Results;
using BunkFy.Modules.Ingestion.Domain.Errors;

public sealed class ObservationReceipt : ScopedAggregateRoot<Guid>
{
    public const int SourceRecordTypeMaxLength = AdapterProtocolLimits.RecordTypeMaxLength;
    public const int ExternalIdMaxLength = AdapterProtocolLimits.ExternalRecordIdMaxLength;
    public const int SourceRevisionMaxLength = AdapterProtocolLimits.SourceRevisionMaxLength;
    public const int DeduplicationKeyMaxLength = 512;
    public const int ContentHashLength = AdapterProtocolLimits.Sha256Length;
    public const int RejectionReasonMaxLength = 2000;
    public const int ParserTypeMaxLength = 100;

    private ObservationReceipt() { }

    private ObservationReceipt(Guid id, string scopeId)
        : base(id, scopeId)
    {
    }

    public Guid PropertyId { get; private set; }
    public Guid ConnectionId { get; private set; }
    public Guid? RunId { get; private set; }
    public Guid OperationId { get; private set; }
    public string SourceRecordType { get; private set; } = string.Empty;
    public string ExternalId { get; private set; } = string.Empty;
    public string? SourceRevision { get; private set; }
    public string DeduplicationKey { get; private set; } = string.Empty;
    public string ContentHash { get; private set; } = string.Empty;
    public Guid RawPayloadFileId { get; private set; }
    public RawPayloadRetentionState RawPayloadRetentionState { get; private set; } = RawPayloadRetentionState.Available;
    public DateTimeOffset RawPayloadRetainUntilUtc { get; private set; }
    public Guid? RawPayloadPurgeClaimId { get; private set; }
    public DateTimeOffset? RawPayloadPurgeStartedAtUtc { get; private set; }
    public DateTimeOffset? RawPayloadPurgedAtUtc { get; private set; }
    public long RawPayloadVersion { get; private set; } = 1;
    public Guid? ActiveReprocessingAttemptId { get; private set; }
    public DateTimeOffset? ReprocessingReservationExpiresAtUtc { get; private set; }
    public Guid? SourceReceiptId { get; private set; }
    public Guid? ReprocessingAttemptId { get; private set; }
    public string? ParserType { get; private set; }
    public int? ParserVersion { get; private set; }
    public int? ParserOutputIndex { get; private set; }
    public DateTimeOffset? SourceUpdatedAtUtc { get; private set; }
    public DateTimeOffset ObservedAtUtc { get; private set; }
    public ObservationReceiptState State { get; private set; } = ObservationReceiptState.Pending;
    public string? RejectionReason { get; private set; }
    public DateTimeOffset ReceivedAtUtc { get; private set; }
    public DateTimeOffset? ProcessedAtUtc { get; private set; }

    public static Result<ObservationReceipt> Create(
        Guid receiptId,
        string scopeId,
        Guid propertyId,
        Guid connectionId,
        Guid? runId,
        Guid operationId,
        string sourceRecordType,
        string externalId,
        string? sourceRevision,
        string deduplicationKey,
        string contentHash,
        Guid rawPayloadFileId,
        DateTimeOffset rawPayloadRetainUntilUtc,
        DateTimeOffset? sourceUpdatedAtUtc,
        DateTimeOffset observedAtUtc,
        DateTimeOffset receivedAtUtc,
        Guid? sourceReceiptId = null,
        Guid? reprocessingAttemptId = null,
        string? parserType = null,
        int? parserVersion = null,
        int? parserOutputIndex = null)
    {
        if (receiptId == Guid.Empty || runId == Guid.Empty || operationId == Guid.Empty)
        {
            return Result.Failure<ObservationReceipt>(IngestionDomainErrors.ReceiptIdentityInvalid);
        }

        if (string.IsNullOrWhiteSpace(scopeId))
        {
            return Result.Failure<ObservationReceipt>(IngestionDomainErrors.ScopeRequired);
        }

        if (propertyId == Guid.Empty)
        {
            return Result.Failure<ObservationReceipt>(IngestionDomainErrors.PropertyIdRequired);
        }

        if (connectionId == Guid.Empty)
        {
            return Result.Failure<ObservationReceipt>(IngestionDomainErrors.ConnectionIdRequired);
        }

        string normalizedRecordType = sourceRecordType?.Trim().ToLowerInvariant() ?? string.Empty;
        string normalizedExternalId = externalId?.Trim() ?? string.Empty;
        string? normalizedRevision = string.IsNullOrWhiteSpace(sourceRevision) ? null : sourceRevision.Trim();
        string normalizedDeduplicationKey = deduplicationKey?.Trim() ?? string.Empty;
        if (normalizedRecordType.Length is 0 or > SourceRecordTypeMaxLength ||
            normalizedExternalId.Length is 0 or > ExternalIdMaxLength ||
            normalizedRevision?.Length > SourceRevisionMaxLength ||
            normalizedDeduplicationKey.Length is 0 or > DeduplicationKeyMaxLength)
        {
            return Result.Failure<ObservationReceipt>(IngestionDomainErrors.ReceiptIdentityInvalid);
        }

        string normalizedHash = contentHash?.Trim().ToLowerInvariant() ?? string.Empty;
        if (rawPayloadFileId == Guid.Empty || normalizedHash.Length != ContentHashLength || !normalizedHash.All(Uri.IsHexDigit))
        {
            return Result.Failure<ObservationReceipt>(IngestionDomainErrors.PayloadInvalid);
        }

        if (rawPayloadRetainUntilUtc <= receivedAtUtc)
        {
            return Result.Failure<ObservationReceipt>(IngestionDomainErrors.RawPayloadRetentionInvalid);
        }

        bool hasAnyLineage = sourceReceiptId.HasValue || reprocessingAttemptId.HasValue || parserType is not null ||
                             parserVersion.HasValue || parserOutputIndex.HasValue;
        bool hasCompleteLineage = sourceReceiptId is { } sourceId && sourceId != Guid.Empty && sourceId != receiptId &&
                                  reprocessingAttemptId is { } attemptId && attemptId != Guid.Empty &&
                                  !string.IsNullOrWhiteSpace(parserType) && parserType.Trim().Length <= ParserTypeMaxLength &&
                                  parserVersion > 0 && parserOutputIndex >= 0;
        if (hasAnyLineage != hasCompleteLineage)
        {
            return Result.Failure<ObservationReceipt>(IngestionDomainErrors.ReprocessingIdentityInvalid);
        }

        return Result.Success(new ObservationReceipt(receiptId, scopeId.Trim())
        {
            PropertyId = propertyId,
            ConnectionId = connectionId,
            RunId = runId,
            OperationId = operationId,
            SourceRecordType = normalizedRecordType,
            ExternalId = normalizedExternalId,
            SourceRevision = normalizedRevision,
            DeduplicationKey = normalizedDeduplicationKey,
            ContentHash = normalizedHash,
            RawPayloadFileId = rawPayloadFileId,
            RawPayloadRetainUntilUtc = rawPayloadRetainUntilUtc,
            SourceUpdatedAtUtc = sourceUpdatedAtUtc,
            ObservedAtUtc = observedAtUtc,
            ReceivedAtUtc = receivedAtUtc,
            SourceReceiptId = sourceReceiptId,
            ReprocessingAttemptId = reprocessingAttemptId,
            ParserType = parserType?.Trim().ToLowerInvariant(),
            ParserVersion = parserVersion,
            ParserOutputIndex = parserOutputIndex
        });
    }

    public Result ReserveForReprocessing(
        Guid attemptId,
        DateTimeOffset reservationExpiresAtUtc,
        DateTimeOffset nowUtc)
    {
        if (attemptId == Guid.Empty || reservationExpiresAtUtc <= nowUtc ||
            this.State != ObservationReceiptState.Rejected ||
            this.RawPayloadRetentionState != RawPayloadRetentionState.Available)
        {
            return Result.Failure(IngestionDomainErrors.ReprocessingSourceInvalid);
        }

        if (this.ActiveReprocessingAttemptId is { } activeId && activeId != attemptId &&
            this.ReprocessingReservationExpiresAtUtc > nowUtc)
        {
            return Result.Failure(IngestionDomainErrors.ReprocessingReservationActive);
        }

        this.ActiveReprocessingAttemptId = attemptId;
        this.ReprocessingReservationExpiresAtUtc = reservationExpiresAtUtc;
        this.RawPayloadVersion++;
        return Result.Success();
    }

    public Result ReleaseReprocessingReservation(Guid attemptId)
    {
        if (attemptId == Guid.Empty)
        {
            return Result.Failure(IngestionDomainErrors.ReprocessingIdentityInvalid);
        }

        if (this.ActiveReprocessingAttemptId is null)
        {
            return Result.Success();
        }

        if (this.ActiveReprocessingAttemptId != attemptId)
        {
            return Result.Failure(IngestionDomainErrors.ReprocessingReservationActive);
        }

        this.ActiveReprocessingAttemptId = null;
        this.ReprocessingReservationExpiresAtUtc = null;
        this.RawPayloadVersion++;
        return Result.Success();
    }

    public Result BeginRawPayloadPurge(
        Guid claimId,
        DateTimeOffset nowUtc,
        DateTimeOffset staleClaimBeforeUtc)
    {
        if (claimId == Guid.Empty || staleClaimBeforeUtc > nowUtc)
        {
            return Result.Failure(IngestionDomainErrors.RawPayloadPurgeClaimInvalid);
        }

        if (this.RawPayloadRetentionState == RawPayloadRetentionState.Purged)
        {
            return Result.Failure(IngestionDomainErrors.RawPayloadAlreadyPurged);
        }

        if (this.ActiveReprocessingAttemptId.HasValue && this.ReprocessingReservationExpiresAtUtc > nowUtc)
        {
            return Result.Failure(IngestionDomainErrors.ReprocessingReservationActive);
        }

        if (this.ActiveReprocessingAttemptId.HasValue)
        {
            this.ActiveReprocessingAttemptId = null;
            this.ReprocessingReservationExpiresAtUtc = null;
        }

        if (this.RawPayloadRetentionState == RawPayloadRetentionState.Purging)
        {
            if (this.RawPayloadPurgeClaimId == claimId)
            {
                return Result.Success();
            }

            if (this.RawPayloadPurgeStartedAtUtc > staleClaimBeforeUtc)
            {
                return Result.Failure(IngestionDomainErrors.RawPayloadPurgeAlreadyClaimed);
            }
        }
        else if (this.State == ObservationReceiptState.Pending || this.RawPayloadRetainUntilUtc > nowUtc)
        {
            return Result.Failure(IngestionDomainErrors.RawPayloadNotPurgeable);
        }

        this.RawPayloadRetentionState = RawPayloadRetentionState.Purging;
        this.RawPayloadPurgeClaimId = claimId;
        this.RawPayloadPurgeStartedAtUtc = nowUtc;
        this.RawPayloadVersion++;
        return Result.Success();
    }

    public Result CompleteRawPayloadPurge(Guid claimId, DateTimeOffset nowUtc)
    {
        if (claimId == Guid.Empty ||
            this.RawPayloadRetentionState != RawPayloadRetentionState.Purging ||
            this.RawPayloadPurgeClaimId != claimId)
        {
            return Result.Failure(IngestionDomainErrors.RawPayloadPurgeClaimInvalid);
        }

        this.RawPayloadRetentionState = RawPayloadRetentionState.Purged;
        this.RawPayloadPurgeClaimId = null;
        this.RawPayloadPurgedAtUtc = nowUtc;
        this.RawPayloadVersion++;
        return Result.Success();
    }

    public Result MarkProcessed(DateTimeOffset nowUtc)
    {
        if (this.State != ObservationReceiptState.Pending)
        {
            return Result.Failure(IngestionDomainErrors.ReceiptNotPending);
        }

        this.State = ObservationReceiptState.Processed;
        this.ProcessedAtUtc = nowUtc;
        return Result.Success();
    }

    public Result Reject(string reason, DateTimeOffset nowUtc)
    {
        if (this.State != ObservationReceiptState.Pending)
        {
            return Result.Failure(IngestionDomainErrors.ReceiptNotPending);
        }

        string normalizedReason = reason?.Trim() ?? string.Empty;
        if (normalizedReason.Length is 0 or > RejectionReasonMaxLength)
        {
            return Result.Failure(IngestionDomainErrors.DecisionReasonInvalid);
        }

        this.State = ObservationReceiptState.Rejected;
        this.RejectionReason = normalizedReason;
        this.ProcessedAtUtc = nowUtc;
        return Result.Success();
    }
}

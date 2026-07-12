namespace BunkFy.Modules.Ingestion.Domain.Reprocessing;

using BunkFy.Adapter.Abstractions;
using Gma.Framework.Domain.Models;
using Gma.Framework.Results;
using BunkFy.Modules.Ingestion.Domain.Errors;

public enum ObservationReprocessingOutputDisposition
{
    Accepted = 1,
    Duplicate = 2,
    Rejected = 3
}

public sealed class ObservationReprocessingOutput : ScopedAggregateRoot<Guid>
{
    public const int RecordTypeMaxLength = AdapterProtocolLimits.RecordTypeMaxLength;
    public const int ExternalIdMaxLength = AdapterProtocolLimits.ExternalRecordIdMaxLength;
    public const int SourceRevisionMaxLength = AdapterProtocolLimits.SourceRevisionMaxLength;
    public const int ContentHashLength = AdapterProtocolLimits.Sha256Length;
    public const int ErrorCodeMaxLength = AdapterProtocolLimits.ErrorCodeMaxLength;

    private ObservationReprocessingOutput() { }

    private ObservationReprocessingOutput(Guid id, string scopeId)
        : base(id, scopeId)
    {
    }

    public Guid AttemptId { get; private set; }
    public int OutputIndex { get; private set; }
    public Guid OperationId { get; private set; }
    public Guid? ReceiptId { get; private set; }
    public ObservationReprocessingOutputDisposition Disposition { get; private set; }
    public string RecordType { get; private set; } = string.Empty;
    public string ExternalId { get; private set; } = string.Empty;
    public string? SourceRevision { get; private set; }
    public string ContentHash { get; private set; } = string.Empty;
    public string? ErrorCode { get; private set; }
    public DateTimeOffset RecordedAtUtc { get; private set; }

    public static Result<ObservationReprocessingOutput> Create(
        Guid operationId,
        string scopeId,
        Guid attemptId,
        int outputIndex,
        Guid? receiptId,
        ObservationReprocessingOutputDisposition disposition,
        string recordType,
        string externalId,
        string? sourceRevision,
        string contentHash,
        string? errorCode,
        DateTimeOffset recordedAtUtc)
    {
        string normalizedScope = scopeId?.Trim() ?? string.Empty;
        string normalizedRecordType = recordType?.Trim().ToLowerInvariant() ?? string.Empty;
        string normalizedExternalId = externalId?.Trim() ?? string.Empty;
        string? normalizedRevision = string.IsNullOrWhiteSpace(sourceRevision) ? null : sourceRevision.Trim();
        string normalizedHash = contentHash?.Trim().ToLowerInvariant() ?? string.Empty;
        string? normalizedError = string.IsNullOrWhiteSpace(errorCode) ? null : errorCode.Trim().ToLowerInvariant();
        bool successDisposition = disposition is ObservationReprocessingOutputDisposition.Accepted or
            ObservationReprocessingOutputDisposition.Duplicate;
        if (operationId == Guid.Empty || attemptId == Guid.Empty || outputIndex < 0 || normalizedScope.Length == 0 ||
            !Enum.IsDefined(disposition) ||
            (successDisposition && receiptId.GetValueOrDefault() == Guid.Empty) ||
            (!successDisposition && receiptId.HasValue) ||
            (successDisposition && normalizedError is not null) ||
            (!successDisposition && normalizedError is null))
        {
            return Result.Failure<ObservationReprocessingOutput>(IngestionDomainErrors.ReprocessingOutcomeInvalid);
        }

        if (normalizedRecordType.Length is 0 or > RecordTypeMaxLength ||
            normalizedExternalId.Length is 0 or > ExternalIdMaxLength ||
            normalizedRevision?.Length > SourceRevisionMaxLength ||
            normalizedHash.Length != ContentHashLength || !normalizedHash.All(Uri.IsHexDigit) ||
            normalizedError?.Length > ErrorCodeMaxLength)
        {
            return Result.Failure<ObservationReprocessingOutput>(IngestionDomainErrors.ReprocessingOutcomeInvalid);
        }

        return Result.Success(new ObservationReprocessingOutput(operationId, normalizedScope)
        {
            AttemptId = attemptId,
            OutputIndex = outputIndex,
            OperationId = operationId,
            ReceiptId = receiptId,
            Disposition = disposition,
            RecordType = normalizedRecordType,
            ExternalId = normalizedExternalId,
            SourceRevision = normalizedRevision,
            ContentHash = normalizedHash,
            ErrorCode = normalizedError,
            RecordedAtUtc = recordedAtUtc
        });
    }

    public bool Matches(
        Guid operationId,
        Guid? receiptId,
        ObservationReprocessingOutputDisposition disposition,
        string recordType,
        string externalId,
        string? sourceRevision,
        string contentHash,
        string? errorCode) =>
        this.OperationId == operationId && this.ReceiptId == receiptId && this.Disposition == disposition &&
        string.Equals(this.RecordType, recordType?.Trim(), StringComparison.OrdinalIgnoreCase) &&
        string.Equals(this.ExternalId, externalId?.Trim(), StringComparison.Ordinal) &&
        string.Equals(this.SourceRevision, string.IsNullOrWhiteSpace(sourceRevision) ? null : sourceRevision.Trim(),
            StringComparison.Ordinal) &&
        string.Equals(this.ContentHash, contentHash?.Trim(), StringComparison.OrdinalIgnoreCase) &&
        string.Equals(this.ErrorCode, string.IsNullOrWhiteSpace(errorCode) ? null : errorCode.Trim(),
            StringComparison.OrdinalIgnoreCase);
}

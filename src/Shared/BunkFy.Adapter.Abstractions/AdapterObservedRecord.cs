namespace BunkFy.Adapter.Abstractions;

public sealed record AdapterObservedRecord
{
    public AdapterObservedRecord(
        Guid operationId,
        string recordType,
        string externalRecordId,
        string? sourceRevision,
        DateTimeOffset? sourceUpdatedAtUtc,
        DateTimeOffset observedAtUtc,
        string contentType,
        byte[] payload,
        string contentSha256)
    {
        this.OperationId = AdapterProtocolGuards.Required(operationId, nameof(operationId));
        this.RecordType = AdapterProtocolGuards.StableKey(
            recordType,
            AdapterProtocolLimits.RecordTypeMaxLength,
            nameof(recordType));
        this.ExternalRecordId = AdapterProtocolGuards.Required(
            externalRecordId,
            AdapterProtocolLimits.ExternalRecordIdMaxLength,
            nameof(externalRecordId));
        this.SourceRevision = AdapterProtocolGuards.Optional(
            sourceRevision,
            AdapterProtocolLimits.SourceRevisionMaxLength,
            nameof(sourceRevision));
        this.ContentType = AdapterProtocolGuards.Required(
            contentType,
            AdapterProtocolLimits.ContentTypeMaxLength,
            nameof(contentType)).ToLowerInvariant();
        ArgumentNullException.ThrowIfNull(payload);
        if (payload.Length is <= 0 or > AdapterProtocolLimits.MaximumInlinePayloadBytes)
        {
            throw new ArgumentException(
                $"Payload must contain between 1 and {AdapterProtocolLimits.MaximumInlinePayloadBytes} bytes.",
                nameof(payload));
        }

        string normalizedHash = AdapterProtocolGuards.Required(
            contentSha256,
            AdapterProtocolLimits.Sha256Length,
            nameof(contentSha256)).ToLowerInvariant();
        if (normalizedHash.Length != AdapterProtocolLimits.Sha256Length ||
            normalizedHash.Any(character => !Uri.IsHexDigit(character)) ||
            !string.Equals(normalizedHash, AdapterPayloadHash.ComputeSha256(payload), StringComparison.Ordinal))
        {
            throw new ArgumentException("contentSha256 must match the payload SHA-256 hash.", nameof(contentSha256));
        }

        this.SourceUpdatedAtUtc = sourceUpdatedAtUtc;
        this.ObservedAtUtc = observedAtUtc;
        this.Payload = payload.ToArray();
        this.ContentSha256 = normalizedHash;
    }

    public Guid OperationId { get; }
    public string RecordType { get; }
    public string ExternalRecordId { get; }
    public string? SourceRevision { get; }
    public DateTimeOffset? SourceUpdatedAtUtc { get; }
    public DateTimeOffset ObservedAtUtc { get; }
    public string ContentType { get; }
    public ReadOnlyMemory<byte> Payload { get; }
    public string ContentSha256 { get; }
}

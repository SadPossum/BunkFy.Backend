namespace BunkFy.ObservationParsing;

using System.Security.Cryptography;
using BunkFy.Adapter.Abstractions;

public sealed record ObservationParserInput : IDisposable
{
    private readonly byte[] payload;

    public ObservationParserInput(
        Guid sourceReceiptId,
        string adapterType,
        string sourceRecordType,
        string externalRecordId,
        string? sourceRevision,
        DateTimeOffset? sourceUpdatedAtUtc,
        DateTimeOffset observedAtUtc,
        string contentType,
        byte[] payload,
        string contentSha256)
    {
        if (sourceReceiptId == Guid.Empty)
        {
            throw new ArgumentException("A source receipt id is required.", nameof(sourceReceiptId));
        }

        this.SourceReceiptId = sourceReceiptId;
        this.AdapterType = ObservationParserGuards.StableKey(
            adapterType, AdapterProtocolLimits.AdapterTypeMaxLength, nameof(adapterType));
        this.SourceRecordType = ObservationParserGuards.StableKey(
            sourceRecordType, AdapterProtocolLimits.RecordTypeMaxLength, nameof(sourceRecordType));
        this.ExternalRecordId = ObservationParserGuards.Required(
            externalRecordId, AdapterProtocolLimits.ExternalRecordIdMaxLength, nameof(externalRecordId));
        this.SourceRevision = ObservationParserGuards.Optional(
            sourceRevision, AdapterProtocolLimits.SourceRevisionMaxLength, nameof(sourceRevision));
        this.ContentType = ObservationParserGuards.Required(
            contentType, AdapterProtocolLimits.ContentTypeMaxLength, nameof(contentType)).ToLowerInvariant();
        ArgumentNullException.ThrowIfNull(payload);
        if (payload.Length is <= 0 or > AdapterProtocolLimits.MaximumInlinePayloadBytes)
        {
            throw new ArgumentException("The parser payload size is outside protocol bounds.", nameof(payload));
        }

        string hash = ObservationParserGuards.Required(
            contentSha256, AdapterProtocolLimits.Sha256Length, nameof(contentSha256)).ToLowerInvariant();
        if (!string.Equals(hash, AdapterPayloadHash.ComputeSha256(payload), StringComparison.Ordinal))
        {
            throw new ArgumentException("The parser payload hash does not match its content.", nameof(contentSha256));
        }

        this.SourceUpdatedAtUtc = sourceUpdatedAtUtc;
        this.ObservedAtUtc = observedAtUtc;
        this.payload = payload.ToArray();
        this.ContentSha256 = hash;
    }

    public Guid SourceReceiptId { get; }
    public string AdapterType { get; }
    public string SourceRecordType { get; }
    public string ExternalRecordId { get; }
    public string? SourceRevision { get; }
    public DateTimeOffset? SourceUpdatedAtUtc { get; }
    public DateTimeOffset ObservedAtUtc { get; }
    public string ContentType { get; }
    public ReadOnlyMemory<byte> Payload => this.payload;
    public string ContentSha256 { get; }

    public void Dispose()
    {
        if (this.payload.Length > 0)
        {
            CryptographicOperations.ZeroMemory(this.payload);
        }
    }
}

namespace BunkFy.Parsers.ReservationMail;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BunkFy.Adapter.Abstractions;
using MimeKit;

public sealed record ReservationMailEnvelopeContent(
    string ExternalRecordId,
    string SourceRevision,
    DateTimeOffset? SourceUpdatedAtUtc,
    byte[] Payload) : IDisposable
{
    public void Dispose()
    {
        if (this.Payload.Length > 0)
        {
            CryptographicOperations.ZeroMemory(this.Payload);
        }
    }
}

public enum ReservationMailAuthenticationDisposition
{
    Untrusted = 1,
    AuthenticatedUnparsed = 2,
    AuthenticatedEnvelope = 3
}

public sealed record ReservationMailAuthenticationResult(
    ReservationMailAuthenticationDisposition Disposition,
    ReservationMailEnvelopeContent? Envelope) : IDisposable
{
    public void Dispose() => this.Envelope?.Dispose();
}

public interface IReservationMailAttachmentKeyResolver
{
    bool TryResolve(string keyId, out ReadOnlyMemory<byte> signingKey);
}

public static class ReservationMailAttachmentSignature
{
    public const string HeaderName = "X-BunkFy-Attachment-Signature";
    public const int MinimumKeyBytes = 32;
    public const int MaximumKeyBytes = 64;
    public const int MaximumKeyIdLength = 64;
    private const int MaximumHeaderLength = 128;
    private const string Prefix = "BunkFy.ImapReservationMail.Attachment.v2\n";
    private static readonly byte[] PrefixBytes = Encoding.ASCII.GetBytes(Prefix);

    public static string Create(
        string keyId,
        ReadOnlySpan<byte> signingKey,
        ReadOnlySpan<byte> attachment)
    {
        ValidateKeyId(keyId);
        ValidateKey(signingKey);
        byte[] signature = Compute(keyId, signingKey, attachment);
        try
        {
            return $"v2={keyId}:{Convert.ToBase64String(signature)}";
        }
        finally
        {
            CryptographicOperations.ZeroMemory(signature);
        }
    }

    public static bool Verify(
        string keyId,
        ReadOnlySpan<byte> signingKey,
        ReadOnlySpan<byte> attachment,
        string signatureHeader)
    {
        ValidateKeyId(keyId);
        ValidateKey(signingKey);
        if (!TryParse(signatureHeader, out string parsedKeyId, out string encodedSignature) ||
            !string.Equals(parsedKeyId, keyId, StringComparison.Ordinal))
        {
            return false;
        }

        Span<byte> expected = stackalloc byte[32];
        if (!Convert.TryFromBase64String(encodedSignature, expected, out int written) || written != expected.Length)
        {
            CryptographicOperations.ZeroMemory(expected);
            return false;
        }

        byte[] actual = Compute(keyId, signingKey, attachment);
        try
        {
            return CryptographicOperations.FixedTimeEquals(expected, actual);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(expected);
            CryptographicOperations.ZeroMemory(actual);
        }
    }

    public static bool TryGetKeyId(string signatureHeader, out string keyId)
    {
        bool parsed = TryParse(signatureHeader, out keyId, out _);
        return parsed;
    }

    public static void ValidateKeyId(string keyId)
    {
        if (!IsValidKeyId(keyId))
        {
            throw new ArgumentException("The reservation-mail signing key ID is invalid.", nameof(keyId));
        }
    }

    public static void ValidateKey(ReadOnlySpan<byte> signingKey)
    {
        if (signingKey.Length is < MinimumKeyBytes or > MaximumKeyBytes)
        {
            throw new ArgumentException("The reservation-mail signing key length is invalid.", nameof(signingKey));
        }
    }

    private static byte[] Compute(
        string keyId,
        ReadOnlySpan<byte> signingKey,
        ReadOnlySpan<byte> attachment)
    {
        using IncrementalHash hmac = IncrementalHash.CreateHMAC(HashAlgorithmName.SHA256, signingKey);
        hmac.AppendData(PrefixBytes);
        hmac.AppendData(Encoding.ASCII.GetBytes(keyId));
        hmac.AppendData("\n"u8);
        hmac.AppendData(attachment);
        return hmac.GetHashAndReset();
    }

    private static bool TryParse(
        string? signatureHeader,
        out string keyId,
        out string encodedSignature)
    {
        keyId = string.Empty;
        encodedSignature = string.Empty;
        if (string.IsNullOrEmpty(signatureHeader) || signatureHeader.Length > MaximumHeaderLength ||
            !signatureHeader.StartsWith("v2=", StringComparison.Ordinal))
        {
            return false;
        }

        int separator = signatureHeader.IndexOf(':', 3);
        if (separator <= 3 || separator == signatureHeader.Length - 1)
        {
            return false;
        }

        string parsedKeyId = signatureHeader[3..separator];
        if (!IsValidKeyId(parsedKeyId))
        {
            return false;
        }

        keyId = parsedKeyId;
        encodedSignature = signatureHeader[(separator + 1)..];
        return true;
    }

    private static bool IsValidKeyId(string? keyId)
    {
        if (string.IsNullOrEmpty(keyId) || keyId.Length > MaximumKeyIdLength ||
            !IsLowerAlphaNumeric(keyId[0]))
        {
            return false;
        }

        return keyId.All(character => IsLowerAlphaNumeric(character) || character is '.' or '_' or '-');
    }

    private static bool IsLowerAlphaNumeric(char character) =>
        character is (>= 'a' and <= 'z') or (>= '0' and <= '9');
}

public static class ReservationMailEnvelopeReader
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public static async Task<ReservationMailEnvelopeContent?> TryReadAsync(
        ReadOnlyMemory<byte> messageBytes,
        string? requiredAttachmentFileName,
        int maximumAttachmentBytes,
        CancellationToken cancellationToken)
    {
        using ReservationMailAttachmentContent? attachment = await TryExtractAsync(
            messageBytes,
            requiredAttachmentFileName,
            maximumAttachmentBytes,
            cancellationToken).ConfigureAwait(false);
        return attachment is null ? null : TryParseEnvelope(attachment.Bytes);
    }

    public static async Task<ReservationMailAuthenticationResult> ReadAuthenticatedAsync(
        ReadOnlyMemory<byte> messageBytes,
        string requiredAttachmentFileName,
        int maximumAttachmentBytes,
        IReservationMailAttachmentKeyResolver keyResolver,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(keyResolver);
        using ReservationMailAttachmentContent? attachment = await TryExtractAsync(
            messageBytes,
            requiredAttachmentFileName,
            maximumAttachmentBytes,
            cancellationToken).ConfigureAwait(false);
        if (attachment is null || attachment.SignatureHeaders.Count != 1 ||
            !ReservationMailAttachmentSignature.TryGetKeyId(
                attachment.SignatureHeaders[0],
                out string keyId) ||
            !keyResolver.TryResolve(keyId, out ReadOnlyMemory<byte> signingKey) ||
            !ReservationMailAttachmentSignature.Verify(
                keyId,
                signingKey.Span,
                attachment.Bytes,
                attachment.SignatureHeaders[0]))
        {
            return new(ReservationMailAuthenticationDisposition.Untrusted, Envelope: null);
        }

        ReservationMailEnvelopeContent? envelope = TryParseEnvelope(attachment.Bytes);
        return envelope is null
            ? new(ReservationMailAuthenticationDisposition.AuthenticatedUnparsed, Envelope: null)
            : new(ReservationMailAuthenticationDisposition.AuthenticatedEnvelope, envelope);
    }

    private static async Task<ReservationMailAttachmentContent?> TryExtractAsync(
        ReadOnlyMemory<byte> messageBytes,
        string? requiredAttachmentFileName,
        int maximumAttachmentBytes,
        CancellationToken cancellationToken)
    {
        if (messageBytes.IsEmpty || maximumAttachmentBytes <= 0)
        {
            return null;
        }

        byte[] sourceBytes = messageBytes.ToArray();
        try
        {
            using MemoryStream source = new(sourceBytes, writable: false);
            MimeMessage message = await MimeMessage.LoadAsync(source, cancellationToken).ConfigureAwait(false);
            MimePart[] attachments = message.BodyParts
                .OfType<MimePart>()
                .Where(part => part.IsAttachment &&
                               string.Equals(part.ContentType.MimeType, "application/json", StringComparison.OrdinalIgnoreCase) &&
                               (requiredAttachmentFileName is null || string.Equals(
                                   part.FileName,
                                   requiredAttachmentFileName,
                                   StringComparison.OrdinalIgnoreCase)))
                .ToArray();
            if (attachments.Length != 1 || attachments[0].Content is not { } content)
            {
                return null;
            }

            using SensitiveBoundedWriteStream decoded = new(maximumAttachmentBytes);
            await content.DecodeToAsync(decoded, cancellationToken).ConfigureAwait(false);
            byte[] bytes = decoded.ToArray();
            if (bytes.Length == 0)
            {
                return null;
            }

            string[] signatures = message.Headers
                .Where(header => string.Equals(header.Field, ReservationMailAttachmentSignature.HeaderName,
                    StringComparison.OrdinalIgnoreCase))
                .Select(header => header.Value)
                .ToArray();
            return new(signatures, bytes);
        }
        catch (Exception exception) when (exception is FormatException or JsonException or
            ArgumentException or InvalidDataException or IOException)
        {
            return null;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(sourceBytes);
        }
    }

    private static ReservationMailEnvelopeContent? TryParseEnvelope(ReadOnlySpan<byte> envelopeBytes)
    {
        try
        {
            ReservationMailEnvelope? envelope = JsonSerializer.Deserialize<ReservationMailEnvelope>(
                envelopeBytes,
                SerializerOptions);
            string? externalRecordId = Normalize(
                envelope?.ExternalRecordId,
                AdapterProtocolLimits.ExternalRecordIdMaxLength);
            string? sourceRevision = Normalize(
                envelope?.SourceRevision,
                AdapterProtocolLimits.SourceRevisionMaxLength);
            if (envelope is null || envelope.SchemaVersion != 1 ||
                externalRecordId is null || sourceRevision is null ||
                envelope.Payload.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            {
                return null;
            }

            byte[] payload = Encoding.UTF8.GetBytes(envelope.Payload.GetRawText());
            return new(
                externalRecordId,
                sourceRevision,
                envelope.SourceUpdatedAtUtc,
                payload);
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException)
        {
            return null;
        }
    }

    private static string? Normalize(string? value, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string normalized = value.Trim();
        return normalized.Length <= maximumLength && !normalized.Any(char.IsControl)
            ? normalized
            : null;
    }

    private sealed record ReservationMailAttachmentContent(
        IReadOnlyList<string> SignatureHeaders,
        byte[] Bytes) : IDisposable
    {
        public void Dispose() => CryptographicOperations.ZeroMemory(this.Bytes);
    }

    private sealed class SensitiveBoundedWriteStream(int maximumBytes) : Stream
    {
        private readonly MemoryStream buffer = new(Math.Min(maximumBytes, 64 * 1024));

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => this.buffer.Length;
        public override long Position
        {
            get => this.buffer.Position;
            set => throw new NotSupportedException();
        }

        public byte[] ToArray() => this.buffer.ToArray();

        public override void Flush()
        {
        }

        public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public override void Write(byte[] buffer, int offset, int count)
        {
            this.EnsureCapacity(count);
            this.buffer.Write(buffer, offset, count);
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            this.EnsureCapacity(buffer.Length);
            this.buffer.Write(buffer);
        }

        public override ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            this.EnsureCapacity(buffer.Length);
            this.buffer.Write(buffer.Span);
            return ValueTask.CompletedTask;
        }

        public override void WriteByte(byte value)
        {
            this.EnsureCapacity(1);
            this.buffer.WriteByte(value);
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (this.buffer.TryGetBuffer(out ArraySegment<byte> bytes) && bytes.Array is not null)
                {
                    CryptographicOperations.ZeroMemory(bytes.Array);
                }

                this.buffer.Dispose();
            }

            base.Dispose(disposing);
        }

        private void EnsureCapacity(int additionalBytes)
        {
            if (additionalBytes < 0 || this.buffer.Length + additionalBytes > maximumBytes)
            {
                throw new InvalidDataException("The reservation-mail attachment exceeded its configured bound.");
            }
        }
    }

    private sealed class ReservationMailEnvelope
    {
        [JsonPropertyName("schemaVersion")]
        public int SchemaVersion { get; init; }

        [JsonPropertyName("externalRecordId")]
        public string? ExternalRecordId { get; init; }

        [JsonPropertyName("sourceRevision")]
        public string? SourceRevision { get; init; }

        [JsonPropertyName("sourceUpdatedAtUtc")]
        public DateTimeOffset? SourceUpdatedAtUtc { get; init; }

        [JsonPropertyName("payload")]
        public JsonElement Payload { get; init; }
    }
}

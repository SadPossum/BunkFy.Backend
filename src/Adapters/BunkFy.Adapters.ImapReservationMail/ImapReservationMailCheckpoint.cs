namespace BunkFy.Adapters.ImapReservationMail;

using System.Text.Json;
using System.Text.Json.Serialization;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using BunkFy.Adapter.Abstractions;

internal readonly record struct ImapReservationMailCheckpoint(
    string MailboxKey,
    uint UidValidity,
    uint LastUid)
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public static ImapReservationMailCheckpoint? Parse(string? checkpoint)
    {
        if (string.IsNullOrWhiteSpace(checkpoint))
        {
            return null;
        }

        if (checkpoint.Length > AdapterProtocolLimits.CheckpointMaxLength)
        {
            throw new InvalidOperationException("The IMAP reservation-mail checkpoint is too large.");
        }

        try
        {
            CheckpointDocument? document = JsonSerializer.Deserialize<CheckpointDocument>(
                checkpoint,
                SerializerOptions);
            if (document is null || document.SchemaVersion != 1 || document.UidValidity == 0 ||
                document.MailboxKey is null ||
                document.MailboxKey.Length != AdapterProtocolLimits.Sha256Length ||
                document.MailboxKey.Any(character => !Uri.IsHexDigit(character)))
            {
                throw new InvalidOperationException("The IMAP reservation-mail checkpoint is invalid.");
            }

            return new(document.MailboxKey.ToLowerInvariant(), document.UidValidity, document.LastUid);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                "The IMAP reservation-mail checkpoint is invalid.",
                exception);
        }
    }

    public string Serialize() => JsonSerializer.Serialize(new CheckpointDocument
    {
        SchemaVersion = 1,
        MailboxKey = this.MailboxKey,
        UidValidity = this.UidValidity,
        LastUid = this.LastUid
    }, SerializerOptions);

    public static string CreateMailboxKey(
        ImapReservationMailSettings settings,
        ImapCredential credential)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(credential);
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Append(hash, settings.Host);
        Append(hash, settings.Port.ToString(CultureInfo.InvariantCulture));
        Append(hash, settings.Mailbox);
        Append(hash, credential.Username);
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    private static void Append(IncrementalHash hash, string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        Span<byte> length = stackalloc byte[sizeof(int)];
        BitConverter.TryWriteBytes(length, bytes.Length);
        hash.AppendData(length);
        hash.AppendData(bytes);
    }

    private sealed class CheckpointDocument
    {
        [JsonPropertyName("schemaVersion")]
        public int SchemaVersion { get; init; }

        [JsonPropertyName("uidValidity")]
        public uint UidValidity { get; init; }

        [JsonPropertyName("mailboxKey")]
        public string? MailboxKey { get; init; }

        [JsonPropertyName("lastUid")]
        public uint LastUid { get; init; }
    }
}

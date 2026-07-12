namespace BunkFy.Adapters.ImapReservationMail;

using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using BunkFy.Adapter.Abstractions;
using BunkFy.Parsers.ReservationMail;
using MailKit.Security;

internal sealed record ImapReservationMailSettings(
    string Host,
    int Port,
    string Mailbox,
    string AttachmentFileName,
    SecureSocketOptions SocketOptions,
    TimeSpan NetworkTimeout,
    int MaximumMessagesPerRun,
    int MaximumMessageBytes,
    int MaximumAttachmentBytes);

internal enum ImapAuthenticationKind
{
    Password = 1,
    OAuth2 = 2
}

internal sealed class ImapObservationSigningKey(
    string keyId,
    byte[] key) : IDisposable
{
    public string KeyId { get; } = keyId;
    public byte[] Key { get; } = key;

    public override string ToString() => nameof(ImapObservationSigningKey);

    public void Dispose()
    {
        if (this.Key.Length > 0)
        {
            CryptographicOperations.ZeroMemory(this.Key);
        }
    }
}

internal sealed class ImapCredential(
    ImapAuthenticationKind authentication,
    string username,
    string credential,
    IReadOnlyList<ImapObservationSigningKey> observationSigningKeys)
    : IReservationMailAttachmentKeyResolver, IDisposable
{
    private readonly Dictionary<string, ImapObservationSigningKey> signingKeysById =
        observationSigningKeys.ToDictionary(item => item.KeyId, StringComparer.Ordinal);
    public ImapAuthenticationKind Authentication { get; } = authentication;
    public string Username { get; } = username;
    public string Credential { get; } = credential;
    public IReadOnlyList<ImapObservationSigningKey> ObservationSigningKeys { get; } = observationSigningKeys;

    public override string ToString() => nameof(ImapCredential);

    public bool TryResolve(string keyId, out ReadOnlyMemory<byte> signingKey)
    {
        if (this.signingKeysById.TryGetValue(keyId, out ImapObservationSigningKey? found))
        {
            signingKey = found.Key;
            return true;
        }

        signingKey = default;
        return false;
    }

    public void Dispose()
    {
        foreach (ImapObservationSigningKey signingKey in this.ObservationSigningKeys)
        {
            signingKey.Dispose();
        }
    }
}

internal static class ImapReservationMailMaterial
{
    private const int MaximumHostLength = 253;
    private const int MaximumMailboxLength = 256;
    private const int MaximumFileNameLength = 128;
    private const int MaximumUsernameLength = 320;
    private const int MaximumSigningKeyTextLength = 128;
    private const int MaximumSigningKeys = 4;
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public static (ImapReservationMailSettings Settings, ImapCredential Credential) Parse(
        AdapterConfigurationMaterial material)
    {
        ArgumentNullException.ThrowIfNull(material);
        if (material.SchemaVersion != ImapReservationMailAdapterDescriptor.Value.ConfigurationSchemaVersion ||
            !string.Equals(material.ConfigurationContentType, "application/json", StringComparison.Ordinal) ||
            !material.HasSecret ||
            !string.Equals(material.SecretContentType, "application/json", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The IMAP reservation-mail material is incompatible.");
        }

        ConfigurationDocument configuration = Deserialize<ConfigurationDocument>(
            material.Configuration.Span,
            "configuration");
        SecretDocument secret = Deserialize<SecretDocument>(material.Secret.Span, "secret");
        return (Validate(configuration), Validate(secret));
    }

    private static ImapReservationMailSettings Validate(ConfigurationDocument document)
    {
        string host = Required(document.Host, MaximumHostLength, "host").ToLowerInvariant();
        if (Uri.CheckHostName(host) == UriHostNameType.Unknown || host.Contains('%'))
        {
            throw Invalid("host");
        }

        if (document.Port is < 1 or > 65535)
        {
            throw Invalid("port");
        }

        string mailbox = Required(document.Mailbox, MaximumMailboxLength, "mailbox");
        string attachmentFileName = Required(
            document.AttachmentFileName,
            MaximumFileNameLength,
            "attachmentFileName");
        if (attachmentFileName.IndexOfAny(['/', '\\']) >= 0 ||
            !string.Equals(Path.GetFileName(attachmentFileName), attachmentFileName, StringComparison.Ordinal))
        {
            throw Invalid("attachmentFileName");
        }

        SecureSocketOptions socketOptions = document.TransportSecurity switch
        {
            "tls" => SecureSocketOptions.SslOnConnect,
            "starttls" => SecureSocketOptions.StartTls,
            "none" when document.AllowInsecureLoopback && IsLiteralLoopback(host) => SecureSocketOptions.None,
            _ => throw Invalid("transportSecurity")
        };
        if (document.NetworkTimeoutSeconds is < 5 or > 120 ||
            document.MaximumMessagesPerRun is < 1 or > AdapterProtocolLimits.MaximumRecordsPerSubmission ||
            document.MaximumMessageBytes is < 1024 or > AdapterProtocolLimits.MaximumInlinePayloadBytes ||
            document.MaximumAttachmentBytes is < 1 or > AdapterProtocolLimits.MaximumInlinePayloadBytes ||
            document.MaximumAttachmentBytes > document.MaximumMessageBytes)
        {
            throw new InvalidOperationException("The IMAP reservation-mail limits are invalid.");
        }

        return new(
            host,
            document.Port,
            mailbox,
            attachmentFileName,
            socketOptions,
            TimeSpan.FromSeconds(document.NetworkTimeoutSeconds),
            document.MaximumMessagesPerRun,
            document.MaximumMessageBytes,
            document.MaximumAttachmentBytes);
    }

    private static ImapCredential Validate(SecretDocument document)
    {
        string username = Required(document.Username, MaximumUsernameLength, "username");
        string? credential = document.Credential;
        if (string.IsNullOrEmpty(credential) ||
            credential.Length > AdapterProtocolLimits.MaximumSecretMaterialBytes - 1024 ||
            credential.Contains('\0'))
        {
            throw Invalid("credential");
        }

        ImapAuthenticationKind authentication = document.Authentication switch
        {
            "password" => ImapAuthenticationKind.Password,
            "oauth2" => ImapAuthenticationKind.OAuth2,
            _ => throw Invalid("authentication")
        };
        if (authentication == ImapAuthenticationKind.OAuth2 && credential.Any(char.IsWhiteSpace))
        {
            throw Invalid("credential");
        }

        if (document.ObservationSigningKeys is not { Length: > 0 } keyDocuments ||
            keyDocuments.Length > MaximumSigningKeys)
        {
            throw Invalid("observationSigningKeys");
        }

        List<ImapObservationSigningKey> signingKeys = new(keyDocuments.Length);
        try
        {
            HashSet<string> keyIds = new(StringComparer.Ordinal);
            foreach (SigningKeyDocument? keyDocument in keyDocuments)
            {
                if (keyDocument is null)
                {
                    throw Invalid("observationSigningKeys");
                }

                string keyId = keyDocument.KeyId ?? string.Empty;
                ReservationMailAttachmentSignature.ValidateKeyId(keyId);
                if (!keyIds.Add(keyId))
                {
                    throw new InvalidOperationException("A reservation-mail signing key ID is duplicated.");
                }

                string signingKeyText = keyDocument.Key ?? string.Empty;
                if (signingKeyText.Length is <= 0 or > MaximumSigningKeyTextLength ||
                    signingKeyText.Any(char.IsWhiteSpace))
                {
                    throw new InvalidOperationException("A reservation-mail signing key is invalid.");
                }

                byte[] key = Convert.FromBase64String(signingKeyText);
                try
                {
                    ReservationMailAttachmentSignature.ValidateKey(key);
                    if (signingKeys.Any(existing => existing.Key.AsSpan().SequenceEqual(key)))
                    {
                        throw new InvalidOperationException("Reservation-mail signing key material is duplicated.");
                    }

                    signingKeys.Add(new(keyId, key));
                }
                catch
                {
                    CryptographicOperations.ZeroMemory(key);
                    throw;
                }
            }
        }
        catch (Exception exception) when (exception is FormatException or ArgumentException)
        {
            foreach (ImapObservationSigningKey signingKey in signingKeys)
            {
                signingKey.Dispose();
            }

            throw Invalid("observationSigningKeys");
        }
        catch (InvalidOperationException)
        {
            foreach (ImapObservationSigningKey signingKey in signingKeys)
            {
                signingKey.Dispose();
            }

            throw Invalid("observationSigningKeys");
        }

        return new(authentication, username, credential, signingKeys);
    }

    private static T Deserialize<T>(ReadOnlySpan<byte> bytes, string kind)
        where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(bytes, SerializerOptions) ??
                throw new InvalidOperationException($"The IMAP reservation-mail {kind} is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                $"The IMAP reservation-mail {kind} is invalid.",
                exception);
        }
    }

    private static string Required(string? value, int maximumLength, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw Invalid(name);
        }

        string normalized = value.Trim();
        if (normalized.Length > maximumLength || normalized.Any(char.IsControl))
        {
            throw Invalid(name);
        }

        return normalized;
    }

    private static bool IsLiteralLoopback(string host) =>
        string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) ||
        (IPAddress.TryParse(host, out IPAddress? address) && IPAddress.IsLoopback(address));

    private static InvalidOperationException Invalid(string name) =>
        new($"The IMAP reservation-mail {name} is invalid.");

    private sealed class ConfigurationDocument
    {
        [JsonPropertyName("host")]
        public string? Host { get; init; }

        [JsonPropertyName("port")]
        public int Port { get; init; }

        [JsonPropertyName("mailbox")]
        public string? Mailbox { get; init; }

        [JsonPropertyName("attachmentFileName")]
        public string? AttachmentFileName { get; init; }

        [JsonPropertyName("transportSecurity")]
        public string? TransportSecurity { get; init; }

        [JsonPropertyName("allowInsecureLoopback")]
        public bool AllowInsecureLoopback { get; init; }

        [JsonPropertyName("networkTimeoutSeconds")]
        public int NetworkTimeoutSeconds { get; init; } = 30;

        [JsonPropertyName("maximumMessagesPerRun")]
        public int MaximumMessagesPerRun { get; init; } = 25;

        [JsonPropertyName("maximumMessageBytes")]
        public int MaximumMessageBytes { get; init; } = AdapterProtocolLimits.MaximumInlinePayloadBytes;

        [JsonPropertyName("maximumAttachmentBytes")]
        public int MaximumAttachmentBytes { get; init; } = 1024 * 1024;
    }

    private sealed class SecretDocument
    {
        [JsonPropertyName("authentication")]
        public string? Authentication { get; init; }

        [JsonPropertyName("username")]
        public string? Username { get; init; }

        [JsonPropertyName("credential")]
        public string? Credential { get; init; }

        [JsonPropertyName("observationSigningKeys")]
        public SigningKeyDocument[]? ObservationSigningKeys { get; init; }
    }

    private sealed class SigningKeyDocument
    {
        [JsonPropertyName("keyId")]
        public string? KeyId { get; init; }

        [JsonPropertyName("key")]
        public string? Key { get; init; }
    }
}

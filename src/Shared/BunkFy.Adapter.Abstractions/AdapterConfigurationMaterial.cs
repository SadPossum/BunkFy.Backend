namespace BunkFy.Adapter.Abstractions;

using System.Security.Cryptography;

public sealed class AdapterConfigurationMaterial : IDisposable
{
    private byte[] configuration;
    private byte[]? secret;
    private bool disposed;

    public AdapterConfigurationMaterial(
        int schemaVersion,
        string configurationContentType,
        ReadOnlySpan<byte> configuration,
        string? secretContentType = null,
        ReadOnlySpan<byte> secret = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(schemaVersion);
        if (configuration.Length is <= 0 or > AdapterProtocolLimits.MaximumConfigurationMaterialBytes)
        {
            throw new ArgumentException(
                $"Configuration material must contain between 1 and {AdapterProtocolLimits.MaximumConfigurationMaterialBytes} bytes.",
                nameof(configuration));
        }

        if (secret.Length > AdapterProtocolLimits.MaximumSecretMaterialBytes)
        {
            throw new ArgumentException(
                $"Secret material cannot exceed {AdapterProtocolLimits.MaximumSecretMaterialBytes} bytes.",
                nameof(secret));
        }

        if (secret.Length > 0 && string.IsNullOrWhiteSpace(secretContentType))
        {
            throw new ArgumentException("Secret content type is required when secret material is present.", nameof(secretContentType));
        }

        this.SchemaVersion = schemaVersion;
        this.ConfigurationContentType = NormalizeContentType(configurationContentType, nameof(configurationContentType));
        this.SecretContentType = secret.Length == 0
            ? null
            : NormalizeContentType(secretContentType!, nameof(secretContentType));
        this.configuration = configuration.ToArray();
        this.secret = secret.Length == 0 ? null : secret.ToArray();
    }

    public int SchemaVersion { get; }
    public string ConfigurationContentType { get; }
    public string? SecretContentType { get; }
    public bool HasSecret => !this.disposed && this.secret is not null;

    public ReadOnlyMemory<byte> Configuration
    {
        get
        {
            this.ThrowIfDisposed();
            return this.configuration;
        }
    }

    public ReadOnlyMemory<byte> Secret
    {
        get
        {
            this.ThrowIfDisposed();
            return this.secret ?? ReadOnlyMemory<byte>.Empty;
        }
    }

    public void Dispose()
    {
        if (this.disposed)
        {
            return;
        }

        CryptographicOperations.ZeroMemory(this.configuration);
        if (this.secret is not null)
        {
            CryptographicOperations.ZeroMemory(this.secret);
        }

        this.configuration = [];
        this.secret = null;
        this.disposed = true;
    }

    public override string ToString() =>
        $"{nameof(AdapterConfigurationMaterial)} {{ SchemaVersion = {this.SchemaVersion}, ConfigurationContentType = {this.ConfigurationContentType}, HasSecret = {this.HasSecret} }}";

    private static string NormalizeContentType(string contentType, string parameterName) =>
        AdapterProtocolGuards.Required(
            contentType,
            AdapterProtocolLimits.ContentTypeMaxLength,
            parameterName).ToLowerInvariant();

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(this.disposed, this);
}

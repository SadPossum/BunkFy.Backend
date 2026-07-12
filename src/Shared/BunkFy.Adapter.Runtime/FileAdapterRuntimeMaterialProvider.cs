namespace BunkFy.Adapter.Runtime;

using System.Security.Cryptography;
using BunkFy.Adapter.Abstractions;

public sealed record FileAdapterRuntimeMaterialOptions
{
    public FileAdapterRuntimeMaterialOptions(
        string configurationFilePath,
        string configurationContentType,
        string? secretFilePath = null,
        string? secretContentType = null)
    {
        this.ConfigurationFilePath = NormalizePath(configurationFilePath, nameof(configurationFilePath));
        this.ConfigurationContentType = NormalizeContentType(
            configurationContentType,
            nameof(configurationContentType));
        this.SecretFilePath = string.IsNullOrWhiteSpace(secretFilePath)
            ? null
            : NormalizePath(secretFilePath, nameof(secretFilePath));
        this.SecretContentType = this.SecretFilePath is null
            ? null
            : NormalizeContentType(secretContentType, nameof(secretContentType));
    }

    public string ConfigurationFilePath { get; }
    public string ConfigurationContentType { get; }
    public string? SecretFilePath { get; }
    public string? SecretContentType { get; }

    private static string NormalizePath(string? path, string parameterName)
    {
        string selected = path?.Trim() ?? string.Empty;
        if (selected.Length == 0)
        {
            throw new ArgumentException("A material file path is required.", parameterName);
        }

        string fullPath = Path.GetFullPath(selected);
        if (string.IsNullOrWhiteSpace(Path.GetFileName(fullPath)))
        {
            throw new ArgumentException("A material path must identify a file.", parameterName);
        }

        return fullPath;
    }

    private static string NormalizeContentType(string? contentType, string parameterName)
    {
        string selected = contentType?.Trim().ToLowerInvariant() ?? string.Empty;
        if (selected.Length is 0 or > AdapterProtocolLimits.ContentTypeMaxLength)
        {
            throw new ArgumentException("A valid material content type is required.", parameterName);
        }

        return selected;
    }
}

public sealed class FileAdapterRuntimeMaterialProvider(FileAdapterRuntimeMaterialOptions options)
    : IAdapterRuntimeMaterialProvider
{
    public async Task<AdapterConfigurationMaterial> ResolveAsync(
        AdapterRuntimeIdentity identity,
        int configurationSchemaVersion,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(configurationSchemaVersion);
        byte[] configuration = [];
        byte[] secret = [];
        try
        {
            configuration = await ReadBoundedAsync(
                options.ConfigurationFilePath,
                AdapterProtocolLimits.MaximumConfigurationMaterialBytes,
                required: true,
                cancellationToken).ConfigureAwait(false);
            secret = options.SecretFilePath is null
                ? []
                : await ReadBoundedAsync(
                    options.SecretFilePath,
                    AdapterProtocolLimits.MaximumSecretMaterialBytes,
                    required: false,
                    cancellationToken).ConfigureAwait(false);
            return new AdapterConfigurationMaterial(
                configurationSchemaVersion,
                options.ConfigurationContentType,
                configuration,
                options.SecretContentType,
                secret);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(configuration);
            CryptographicOperations.ZeroMemory(secret);
        }
    }

    private static async Task<byte[]> ReadBoundedAsync(
        string path,
        int maximumBytes,
        bool required,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            throw new AdapterRuntimeProtocolException("An adapter material file is unavailable.");
        }

        RejectReparsePath(path);
        await using FileStream stream = new(path, new FileStreamOptions
        {
            Mode = FileMode.Open,
            Access = FileAccess.Read,
            Share = FileShare.Read,
            Options = FileOptions.Asynchronous | FileOptions.SequentialScan
        });
        if (stream.Length > maximumBytes)
        {
            throw new AdapterRuntimeProtocolException("An adapter material file exceeds its size limit.");
        }

        byte[] content = new byte[checked((int)stream.Length)];
        int offset = 0;
        while (offset < content.Length)
        {
            int read = await stream.ReadAsync(content.AsMemory(offset), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            offset += read;
        }

        if (offset != content.Length || (required && content.Length == 0))
        {
            CryptographicOperations.ZeroMemory(content);
            throw new AdapterRuntimeProtocolException("An adapter material file could not be read completely.");
        }

        return content;
    }

    private static void RejectReparsePath(string path)
    {
        FileSystemInfo? current = new FileInfo(path);
        while (current is not null)
        {
            if ((current.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new AdapterRuntimeProtocolException(
                    "Adapter material paths cannot contain linked or reparse points.");
            }

            current = current switch
            {
                FileInfo file => file.Directory,
                DirectoryInfo directory => directory.Parent,
                _ => null
            };
        }
    }
}

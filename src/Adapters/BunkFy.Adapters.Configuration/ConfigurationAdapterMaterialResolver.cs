namespace BunkFy.Adapters.Configuration;

using System.Security.Cryptography;
using System.Text;
using BunkFy.Adapter.Abstractions;
using Gma.Framework.Results;
using BunkFy.Modules.Ingestion.Contracts.Adapters;
using Microsoft.Extensions.Configuration;

internal sealed class ConfigurationAdapterMaterialResolver(IConfiguration configuration)
    : IAdapterConfigurationMaterialResolver
{
    private const string SectionName = "Adapters:Materials";
    private const string ConfigurationPrefix = "configuration://";
    private const string SecretPrefix = "secret://";
    private const int MaximumReferenceNameLength = 100;

    public Task<Result<AdapterConfigurationMaterial>> ResolveAsync(
        AdapterConfigurationMaterialRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (!TryGetReferenceName(request.ConfigurationReference, ConfigurationPrefix, out string configurationName) ||
            (request.SecretReference is not null &&
             !TryGetReferenceName(request.SecretReference, SecretPrefix, out _)))
        {
            return Task.FromResult(Result.Failure<AdapterConfigurationMaterial>(
                AdapterConfigurationMaterialErrors.ReferenceInvalid));
        }

        IConfigurationSection configurationSection = configuration.GetSection(
            $"{SectionName}:Configurations:{configurationName}");
        string? configurationValue = configurationSection["Value"];
        if (string.IsNullOrEmpty(configurationValue))
        {
            return Task.FromResult(Result.Failure<AdapterConfigurationMaterial>(
                AdapterConfigurationMaterialErrors.MaterialNotFound));
        }

        if (!int.TryParse(configurationSection["SchemaVersion"], out int schemaVersion) ||
            schemaVersion != request.ExpectedSchemaVersion)
        {
            return Task.FromResult(Result.Failure<AdapterConfigurationMaterial>(
                AdapterConfigurationMaterialErrors.SchemaMismatch));
        }

        string configurationContentType = configurationSection["ContentType"] ?? "application/json";
        string? secretValue = null;
        string? secretContentType = null;
        if (request.SecretReference is not null)
        {
            _ = TryGetReferenceName(request.SecretReference, SecretPrefix, out string secretName);
            IConfigurationSection secretSection = configuration.GetSection($"{SectionName}:Secrets:{secretName}");
            secretValue = secretSection["Value"];
            if (string.IsNullOrEmpty(secretValue))
            {
                return Task.FromResult(Result.Failure<AdapterConfigurationMaterial>(
                    AdapterConfigurationMaterialErrors.MaterialNotFound));
            }

            secretContentType = secretSection["ContentType"] ?? "application/json";
        }

        byte[] configurationBytes = Encoding.UTF8.GetBytes(configurationValue);
        byte[] secretBytes = secretValue is null ? [] : Encoding.UTF8.GetBytes(secretValue);
        try
        {
            AdapterConfigurationMaterial material = new(
                schemaVersion,
                configurationContentType,
                configurationBytes,
                secretContentType,
                secretBytes);
            return Task.FromResult(Result.Success(material));
        }
        catch (ArgumentException)
        {
            return Task.FromResult(Result.Failure<AdapterConfigurationMaterial>(
                AdapterConfigurationMaterialErrors.MaterialInvalid));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(configurationBytes);
            CryptographicOperations.ZeroMemory(secretBytes);
        }
    }

    private static bool TryGetReferenceName(string reference, string prefix, out string name)
    {
        name = string.Empty;
        if (string.IsNullOrWhiteSpace(reference) ||
            !reference.StartsWith(prefix, StringComparison.Ordinal) ||
            reference.Length <= prefix.Length)
        {
            return false;
        }

        string candidate = reference[prefix.Length..];
        if (candidate.Length > MaximumReferenceNameLength ||
            !char.IsAsciiLetterOrDigit(candidate[0]) ||
            candidate.Any(character =>
                !char.IsAsciiLetterOrDigit(character) && character is not '.' and not '-' and not '_'))
        {
            return false;
        }

        name = candidate;
        return true;
    }
}

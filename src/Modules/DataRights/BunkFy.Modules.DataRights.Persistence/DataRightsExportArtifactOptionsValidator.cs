namespace BunkFy.Modules.DataRights.Persistence;

using System.Security.Cryptography;
using Microsoft.Extensions.Options;

internal sealed class DataRightsExportArtifactOptionsValidator(bool isProduction)
    : IValidateOptions<DataRightsExportArtifactOptions>
{
    private const int MinimumPlaintextBytes = 1024 * 1024;
    private const int MaximumPlaintextBytes = 256 * 1024 * 1024;
    private const int MinimumChunkBytes = 16 * 1024;
    private const int MaximumChunkBytes = 1024 * 1024;

    public ValidateOptionsResult Validate(
        string? name,
        DataRightsExportArtifactOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        List<string> failures = [];
        if (options.ActiveKeyVersion <= 0)
        {
            failures.Add(
                $"{DataRightsExportArtifactOptions.SectionName}:ActiveKeyVersion must be positive.");
        }

        if (options.MaximumPlaintextBytes is < MinimumPlaintextBytes or >
            MaximumPlaintextBytes)
        {
            failures.Add(
                $"{DataRightsExportArtifactOptions.SectionName}:MaximumPlaintextBytes must be between {MinimumPlaintextBytes} and {MaximumPlaintextBytes}.");
        }

        if (options.ChunkSizeBytes is < MinimumChunkBytes or > MaximumChunkBytes)
        {
            failures.Add(
                $"{DataRightsExportArtifactOptions.SectionName}:ChunkSizeBytes must be between {MinimumChunkBytes} and {MaximumChunkBytes}.");
        }

        if (options.ArtifactLifetime < TimeSpan.FromMinutes(5) ||
            options.ArtifactLifetime > TimeSpan.FromDays(7))
        {
            failures.Add(
                $"{DataRightsExportArtifactOptions.SectionName}:ArtifactLifetime must be between five minutes and seven days.");
        }

        if (options.Keys is null || options.Keys.Count == 0)
        {
            failures.Add(
                $"{DataRightsExportArtifactOptions.SectionName}:Keys must contain at least one versioned key.");
            return ValidateOptionsResult.Fail(failures);
        }

        _ = DataRightsPseudonymisationOptionsValidator.TryDecode(
            DataRightsExportArtifactOptions.DevelopmentKeyBase64,
            out byte[] developmentKey);
        HashSet<string> digests = new(StringComparer.Ordinal);
        foreach ((int version, string encodedKey) in options.Keys)
        {
            if (version <= 0 ||
                !DataRightsPseudonymisationOptionsValidator.TryDecode(
                    encodedKey,
                    out byte[] key))
            {
                failures.Add(
                    $"{DataRightsExportArtifactOptions.SectionName}:Keys:{version} must be a base64-encoded 32-byte key with a positive version.");
                continue;
            }

            try
            {
                if (isProduction &&
                    CryptographicOperations.FixedTimeEquals(key, developmentKey))
                {
                    failures.Add(
                        $"{DataRightsExportArtifactOptions.SectionName}:Keys:{version} uses the development key and is not allowed in Production.");
                }

                string digest = Convert.ToHexStringLower(SHA256.HashData(key));
                if (!digests.Add(digest))
                {
                    failures.Add(
                        $"{DataRightsExportArtifactOptions.SectionName}:Keys must not reuse key material across versions.");
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(key);
            }
        }

        CryptographicOperations.ZeroMemory(developmentKey);
        if (options.ActiveKeyVersion > 0 &&
            !options.Keys.ContainsKey(options.ActiveKeyVersion))
        {
            failures.Add(
                $"{DataRightsExportArtifactOptions.SectionName}:Keys must contain the active key version.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}

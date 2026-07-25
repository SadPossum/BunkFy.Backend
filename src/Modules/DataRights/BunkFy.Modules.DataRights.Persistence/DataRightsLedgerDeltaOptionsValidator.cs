namespace BunkFy.Modules.DataRights.Persistence;

using System.Security.Cryptography;
using Microsoft.Extensions.Options;

internal sealed class DataRightsLedgerDeltaOptionsValidator(bool isProduction)
    : IValidateOptions<DataRightsLedgerDeltaOptions>
{
    internal const int MaximumAllowedPageSize = 1000;

    public ValidateOptionsResult Validate(
        string? name,
        DataRightsLedgerDeltaOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        List<string> failures = [];
        if (!Enum.IsDefined(options.Provider) ||
            options.Provider == DataRightsLedgerDeltaProvider.Unknown)
        {
            failures.Add(
                $"{DataRightsLedgerDeltaOptions.SectionName}:Provider must select LocalFile or External.");
        }

        if (options.MaximumPageSize is <= 0 or > MaximumAllowedPageSize)
        {
            failures.Add(
                $"{DataRightsLedgerDeltaOptions.SectionName}:MaximumPageSize must be between 1 and {MaximumAllowedPageSize}.");
        }

        if (options.Provider == DataRightsLedgerDeltaProvider.LocalFile)
        {
            ValidateLocalFile(options, isProduction, failures);
        }
        else if (options.Provider == DataRightsLedgerDeltaProvider.External)
        {
            if (!string.IsNullOrWhiteSpace(options.LocalFilePath) ||
                options.ActiveIntegrityKeyVersion != 0 ||
                options.IntegrityKeys is { Count: > 0 })
            {
                failures.Add(
                    $"{DataRightsLedgerDeltaOptions.SectionName}:LocalFile settings must be absent when Provider is External.");
            }
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static void ValidateLocalFile(
        DataRightsLedgerDeltaOptions options,
        bool isProduction,
        List<string> failures)
    {
        if (isProduction)
        {
            failures.Add(
                $"{DataRightsLedgerDeltaOptions.SectionName}:Provider LocalFile is not allowed in Production.");
        }

        if (string.IsNullOrWhiteSpace(options.LocalFilePath))
        {
            failures.Add(
                $"{DataRightsLedgerDeltaOptions.SectionName}:LocalFilePath is required for LocalFile.");
        }

        if (options.ActiveIntegrityKeyVersion <= 0)
        {
            failures.Add(
                $"{DataRightsLedgerDeltaOptions.SectionName}:ActiveIntegrityKeyVersion must be positive for LocalFile.");
        }

        if (options.IntegrityKeys is null)
        {
            failures.Add(
                $"{DataRightsLedgerDeltaOptions.SectionName}:IntegrityKeys must contain at least one versioned key for LocalFile.");
            return;
        }

        if (options.IntegrityKeys.Count == 0)
        {
            failures.Add(
                $"{DataRightsLedgerDeltaOptions.SectionName}:IntegrityKeys must contain at least one versioned key for LocalFile.");
        }

        HashSet<string> keyDigests = new(StringComparer.Ordinal);
        foreach ((int version, string encodedKey) in options.IntegrityKeys)
        {
            if (version <= 0 ||
                !DataRightsPseudonymisationOptionsValidator.TryDecode(
                    encodedKey,
                    out byte[] key))
            {
                failures.Add(
                    $"{DataRightsLedgerDeltaOptions.SectionName}:IntegrityKeys:{version} must be a positive version with a base64-encoded {DataRightsPseudonymisationOptionsValidator.KeySizeBytes}-byte key.");
                continue;
            }

            try
            {
                string digest = Convert.ToHexStringLower(SHA256.HashData(key));
                if (!keyDigests.Add(digest))
                {
                    failures.Add(
                        $"{DataRightsLedgerDeltaOptions.SectionName}:IntegrityKeys must not reuse key material across versions.");
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(key);
            }
        }

        if (options.ActiveIntegrityKeyVersion > 0 &&
            !options.IntegrityKeys.ContainsKey(
                options.ActiveIntegrityKeyVersion))
        {
            failures.Add(
                $"{DataRightsLedgerDeltaOptions.SectionName}:IntegrityKeys must contain the active key version.");
        }
    }
}

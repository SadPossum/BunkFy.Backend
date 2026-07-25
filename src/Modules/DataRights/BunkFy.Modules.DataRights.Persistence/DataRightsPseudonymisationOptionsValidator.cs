namespace BunkFy.Modules.DataRights.Persistence;

using System.Security.Cryptography;
using Microsoft.Extensions.Options;

internal sealed class DataRightsPseudonymisationOptionsValidator(bool isProduction)
    : IValidateOptions<DataRightsPseudonymisationOptions>
{
    internal const int KeySizeBytes = 32;

    public ValidateOptionsResult Validate(
        string? name,
        DataRightsPseudonymisationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        List<string> failures = [];
        if (options.ActiveKeyVersion <= 0)
        {
            failures.Add(
                $"{DataRightsPseudonymisationOptions.SectionName}:ActiveKeyVersion must be positive.");
        }

        if (options.Keys is null)
        {
            failures.Add(
                $"{DataRightsPseudonymisationOptions.SectionName}:Keys must contain at least one versioned key.");
            return ValidateOptionsResult.Fail(failures);
        }

        if (options.Keys.Count == 0)
        {
            failures.Add(
                $"{DataRightsPseudonymisationOptions.SectionName}:Keys must contain at least one versioned key.");
        }

        HashSet<string> keyDigests = new(StringComparer.Ordinal);
        _ = TryDecode(
            DataRightsPseudonymisationOptions.DevelopmentKeyBase64,
            out byte[] developmentKey);
        foreach ((int version, string encodedKey) in options.Keys)
        {
            if (version <= 0)
            {
                failures.Add(
                    $"{DataRightsPseudonymisationOptions.SectionName}:Keys contains an invalid version.");
                continue;
            }

            if (!TryDecode(encodedKey, out byte[] key))
            {
                failures.Add(
                    $"{DataRightsPseudonymisationOptions.SectionName}:Keys:{version} must be a base64-encoded {KeySizeBytes}-byte key.");
                continue;
            }

            try
            {
                if (isProduction &&
                    CryptographicOperations.FixedTimeEquals(key, developmentKey))
                {
                    failures.Add(
                        $"{DataRightsPseudonymisationOptions.SectionName}:Keys:{version} uses the development key and is not allowed in Production.");
                }

                string keyDigest = Convert.ToHexStringLower(SHA256.HashData(key));
                if (!keyDigests.Add(keyDigest))
                {
                    failures.Add(
                        $"{DataRightsPseudonymisationOptions.SectionName}:Keys must not reuse key material across versions.");
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
                $"{DataRightsPseudonymisationOptions.SectionName}:Keys must contain the active key version.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    internal static bool TryDecode(string? encodedKey, out byte[] key)
    {
        key = [];
        if (string.IsNullOrWhiteSpace(encodedKey))
        {
            return false;
        }

        try
        {
            key = Convert.FromBase64String(encodedKey.Trim());
            if (key.Length == KeySizeBytes)
            {
                return true;
            }

            CryptographicOperations.ZeroMemory(key);
            key = [];
            return false;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

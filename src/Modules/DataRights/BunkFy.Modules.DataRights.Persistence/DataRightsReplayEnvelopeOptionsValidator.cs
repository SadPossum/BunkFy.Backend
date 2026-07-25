namespace BunkFy.Modules.DataRights.Persistence;

using System.Security.Cryptography;
using Microsoft.Extensions.Options;

internal sealed class DataRightsReplayEnvelopeOptionsValidator(bool isProduction)
    : IValidateOptions<DataRightsReplayEnvelopeOptions>
{
    public ValidateOptionsResult Validate(
        string? name,
        DataRightsReplayEnvelopeOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        List<string> failures = [];
        if (options.ActiveKeyVersion <= 0)
        {
            failures.Add(
                $"{DataRightsReplayEnvelopeOptions.SectionName}:ActiveKeyVersion must be positive.");
        }

        if (options.Keys is null)
        {
            failures.Add(
                $"{DataRightsReplayEnvelopeOptions.SectionName}:Keys must contain at least one versioned key.");
            return ValidateOptionsResult.Fail(failures);
        }

        if (options.Keys.Count == 0)
        {
            failures.Add(
                $"{DataRightsReplayEnvelopeOptions.SectionName}:Keys must contain at least one versioned key.");
        }

        HashSet<string> keyDigests = new(StringComparer.Ordinal);
        _ = DataRightsPseudonymisationOptionsValidator.TryDecode(
            DataRightsReplayEnvelopeOptions.DevelopmentKeyBase64,
            out byte[] developmentKey);
        foreach ((int version, string encodedKey) in options.Keys)
        {
            if (version <= 0)
            {
                failures.Add(
                    $"{DataRightsReplayEnvelopeOptions.SectionName}:Keys contains an invalid version.");
                continue;
            }

            if (!DataRightsPseudonymisationOptionsValidator.TryDecode(
                    encodedKey,
                    out byte[] key))
            {
                failures.Add(
                    $"{DataRightsReplayEnvelopeOptions.SectionName}:Keys:{version} must be a base64-encoded {DataRightsPseudonymisationOptionsValidator.KeySizeBytes}-byte key.");
                continue;
            }

            try
            {
                if (isProduction &&
                    CryptographicOperations.FixedTimeEquals(key, developmentKey))
                {
                    failures.Add(
                        $"{DataRightsReplayEnvelopeOptions.SectionName}:Keys:{version} uses the development key and is not allowed in Production.");
                }

                string digest = Convert.ToHexStringLower(SHA256.HashData(key));
                if (!keyDigests.Add(digest))
                {
                    failures.Add(
                        $"{DataRightsReplayEnvelopeOptions.SectionName}:Keys must not reuse key material across versions.");
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
                $"{DataRightsReplayEnvelopeOptions.SectionName}:Keys must contain the active key version.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}

namespace BunkFy.Modules.Ingestion.Application.Policies;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using BunkFy.DataGovernance;
using BunkFy.Modules.Ingestion.Contracts;

internal static class IngestionAnonymisationPolicyEvidence
{
    public static IngestionAnonymisationRoutingPolicyEvidence FromCurrent(
        long propertyPolicySourceVersion,
        CountryPolicyEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        return new(
            propertyPolicySourceVersion,
            evidence.OperatingCountryCode,
            evidence.PolicyId,
            evidence.PolicyVersion,
            evidence.RetentionPolicyId,
            evidence.RetentionPolicyVersion,
            evidence.ContentSha256,
            evidence.PurposeCode,
            evidence.Surface.ToString(),
            evidence.SourceProvenance,
            evidence.EvaluatedAtUtc);
    }

    public static bool IsValid(
        IngestionAnonymisationRoutingPolicyEvidence? evidence)
    {
        if (evidence is null)
        {
            return false;
        }

        string country = NormalizeUpper(evidence.OperatingCountryCode);
        string digest = NormalizeLower(evidence.ContentSha256);
        return evidence.PropertyPolicySourceVersion > 0 &&
               country.Length == 2 &&
               country.All(character => character is >= 'A' and <= 'Z') &&
               IsKey(NormalizeLower(evidence.PolicyId)) &&
               evidence.PolicyVersion > 0 &&
               IsKey(NormalizeLower(evidence.RetentionPolicyId)) &&
               evidence.RetentionPolicyVersion > 0 &&
               digest.Length ==
                   IngestionAnonymisationEligibilityContract.Sha256Length &&
               digest.All(character =>
                   character is (>= '0' and <= '9') or (>= 'a' and <= 'f')) &&
               IsKey(NormalizeLower(evidence.PurposeCode)) &&
               IsKey(NormalizeLower(evidence.Surface)) &&
               IsKey(NormalizeLower(evidence.SourceProvenance)) &&
               evidence.EvaluatedAtUtc != default;
    }

    public static string ComputeSha256(
        IngestionAnonymisationRoutingPolicyEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        StringBuilder canonical = new();
        Append(
            canonical,
            evidence.PropertyPolicySourceVersion.ToString(
                CultureInfo.InvariantCulture));
        Append(canonical, NormalizeUpper(evidence.OperatingCountryCode));
        Append(canonical, NormalizeLower(evidence.PolicyId));
        Append(
            canonical,
            evidence.PolicyVersion.ToString(CultureInfo.InvariantCulture));
        Append(canonical, NormalizeLower(evidence.RetentionPolicyId));
        Append(
            canonical,
            evidence.RetentionPolicyVersion.ToString(
                CultureInfo.InvariantCulture));
        Append(canonical, NormalizeLower(evidence.ContentSha256));
        Append(canonical, NormalizeLower(evidence.PurposeCode));
        Append(canonical, NormalizeLower(evidence.Surface));
        Append(canonical, NormalizeLower(evidence.SourceProvenance));
        Append(
            canonical,
            evidence.EvaluatedAtUtc.ToUniversalTime().ToString(
                "O",
                CultureInfo.InvariantCulture));
        return Convert.ToHexString(
                SHA256.HashData(
                    Encoding.UTF8.GetBytes(canonical.ToString())))
            .ToLowerInvariant();
    }

    private static void Append(StringBuilder target, string value)
    {
        target.Append(value.Length.ToString(CultureInfo.InvariantCulture));
        target.Append(':');
        target.Append(value);
    }

    private static bool IsKey(string value) =>
        value.Length is > 0 and <= 128 &&
        value[0] is >= 'a' and <= 'z' &&
        value.All(character =>
            character is (>= 'a' and <= 'z') or
                (>= '0' and <= '9') or '.' or '-' or '_');

    private static string NormalizeLower(string? value) =>
        value?.Trim().ToLowerInvariant() ?? string.Empty;

    private static string NormalizeUpper(string? value) =>
        value?.Trim().ToUpperInvariant() ?? string.Empty;
}

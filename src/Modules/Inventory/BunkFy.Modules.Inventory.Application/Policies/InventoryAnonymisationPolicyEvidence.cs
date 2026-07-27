namespace BunkFy.Modules.Inventory.Application.Policies;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using BunkFy.Modules.DataRights.Contracts;

internal static class InventoryAnonymisationPolicyEvidence
{
    public static bool IsValid(DataRightsApprovalEvidence? evidence)
    {
        if (evidence is null)
        {
            return false;
        }

        string country = NormalizeUpper(
            evidence.OperatingCountryCode);
        string digest = NormalizeLower(evidence.ContentSha256);
        return evidence.SchemaVersion > 0 &&
            evidence.PropertyId != Guid.Empty &&
            evidence.PropertyVersion > 0 &&
            country.Length == 2 &&
            country.All(character =>
                character is >= 'A' and <= 'Z') &&
            IsKey(NormalizeLower(evidence.PolicyId)) &&
            evidence.PolicyVersion > 0 &&
            IsKey(NormalizeLower(evidence.RetentionPolicyId)) &&
            evidence.RetentionPolicyVersion > 0 &&
            IsSha256(digest) &&
            IsKey(NormalizeLower(evidence.PurposeCode)) &&
            IsKey(NormalizeLower(evidence.Surface)) &&
            IsKey(NormalizeLower(evidence.SourceProvenance)) &&
            evidence.EvaluatedAtUtc != default;
    }

    public static bool MatchesApproval(
        DataRightsApprovalEvidence requested,
        DataRightsApprovalEvidence approved) =>
        IsValid(requested) &&
        IsValid(approved) &&
        string.Equals(
            ComputeSha256(requested),
            ComputeSha256(approved),
            StringComparison.Ordinal);

    public static string ComputeSha256(
        DataRightsApprovalEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        StringBuilder canonical = new();
        Append(canonical, Invariant(evidence.SchemaVersion));
        Append(canonical, evidence.PropertyId.ToString("N"));
        Append(canonical, Invariant(evidence.PropertyVersion));
        Append(
            canonical,
            NormalizeUpper(evidence.OperatingCountryCode));
        Append(canonical, NormalizeLower(evidence.PolicyId));
        Append(canonical, Invariant(evidence.PolicyVersion));
        Append(
            canonical,
            NormalizeLower(evidence.RetentionPolicyId));
        Append(
            canonical,
            Invariant(evidence.RetentionPolicyVersion));
        Append(
            canonical,
            NormalizeLower(evidence.ContentSha256));
        Append(canonical, NormalizeLower(evidence.PurposeCode));
        Append(canonical, NormalizeLower(evidence.Surface));
        Append(
            canonical,
            NormalizeLower(evidence.SourceProvenance));
        Append(
            canonical,
            evidence.EvaluatedAtUtc.ToUniversalTime().ToString(
                "O",
                CultureInfo.InvariantCulture));
        Append(
            canonical,
            evidence.RequiresDistinctExecutor ? "1" : "0");
        return Convert.ToHexStringLower(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(canonical.ToString())));
    }

    private static void Append(StringBuilder target, string value)
    {
        target.Append(
            value.Length.ToString(CultureInfo.InvariantCulture));
        target.Append(':');
        target.Append(value);
    }

    private static string Invariant(long value) =>
        value.ToString(CultureInfo.InvariantCulture);

    private static bool IsKey(string value) =>
        value.Length is > 0 and <= 128 &&
        value[0] is >= 'a' and <= 'z' &&
        value.All(character =>
            character is (>= 'a' and <= 'z') or
                (>= '0' and <= '9') or '.' or '-' or '_');

    private static bool IsSha256(string value) =>
        value.Length == 64 &&
        value.All(character =>
            character is (>= '0' and <= '9') or
                (>= 'a' and <= 'f'));

    private static string NormalizeLower(string? value) =>
        value?.Trim().ToLowerInvariant() ?? string.Empty;

    private static string NormalizeUpper(string? value) =>
        value?.Trim().ToUpperInvariant() ?? string.Empty;
}

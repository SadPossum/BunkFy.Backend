namespace BunkFy.Modules.DataRights.Contracts;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;

public sealed record TenantTerminationApprovalEvidence(
    string ApprovalReference,
    string OwnerCatalogSha256,
    string BackupEvidenceReference,
    string RestoreDrillEvidenceReference,
    string OperatorAssuranceReference)
{
    public string ComputeSha256() =>
        TenantTerminationApprovalEvidenceContract.ComputeSha256(this);
}

public static class TenantTerminationApprovalEvidenceContract
{
    public const int ReferenceMinLength = 3;
    public const int ReferenceMaxLength = 128;
    private const string DigestDomain =
        "bunkfy.data-rights.tenant-termination.approval-evidence.v1";

    public static bool TryCreate(
        string? approvalReference,
        string? ownerCatalogSha256,
        string? backupEvidenceReference,
        string? restoreDrillEvidenceReference,
        string? operatorAssuranceReference,
        out TenantTerminationApprovalEvidence? evidence)
    {
        string approval = approvalReference?.Trim() ?? string.Empty;
        string ownerCatalog = ownerCatalogSha256?.Trim() ?? string.Empty;
        string backup = backupEvidenceReference?.Trim() ?? string.Empty;
        string restore = restoreDrillEvidenceReference?.Trim() ?? string.Empty;
        string assurance = operatorAssuranceReference?.Trim() ?? string.Empty;
        if (!IsReference(approval) ||
            !IsSha256(ownerCatalog) ||
            !IsReference(backup) ||
            !IsReference(restore) ||
            !IsReference(assurance))
        {
            evidence = null;
            return false;
        }

        evidence = new(
            approval,
            ownerCatalog,
            backup,
            restore,
            assurance);
        return true;
    }

    public static bool IsReference(string? value) =>
        value is { Length: >= ReferenceMinLength and <= ReferenceMaxLength } &&
        value.All(character =>
            character is (>= 'a' and <= 'z') or
                (>= 'A' and <= 'Z') or
                (>= '0' and <= '9') or '.' or '_' or '-' or ':' or '/');

    public static bool IsSha256(string? value) =>
        value is { Length: TenantTerminationContract.Sha256Length } &&
        value.All(character =>
            character is (>= '0' and <= '9') or (>= 'a' and <= 'f'));

    public static bool FixedTimeSha256Equals(string? left, string? right)
    {
        if (!IsSha256(left) || !IsSha256(right))
        {
            return false;
        }

        byte[] leftBytes = Convert.FromHexString(left!);
        byte[] rightBytes = Convert.FromHexString(right!);
        try
        {
            return CryptographicOperations.FixedTimeEquals(
                leftBytes,
                rightBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(leftBytes);
            CryptographicOperations.ZeroMemory(rightBytes);
        }
    }

    public static string ComputeSha256(
        TenantTerminationApprovalEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        if (!TryCreate(
                evidence.ApprovalReference,
                evidence.OwnerCatalogSha256,
                evidence.BackupEvidenceReference,
                evidence.RestoreDrillEvidenceReference,
                evidence.OperatorAssuranceReference,
                out TenantTerminationApprovalEvidence? normalized) ||
            normalized != evidence)
        {
            throw new ArgumentException(
                "The tenant-termination approval evidence is invalid.",
                nameof(evidence));
        }

        StringBuilder canonical = new();
        Append(canonical, DigestDomain);
        Append(canonical, evidence.ApprovalReference);
        Append(canonical, evidence.OwnerCatalogSha256);
        Append(canonical, evidence.BackupEvidenceReference);
        Append(canonical, evidence.RestoreDrillEvidenceReference);
        Append(canonical, evidence.OperatorAssuranceReference);
        byte[] bytes = Encoding.UTF8.GetBytes(canonical.ToString());
        try
        {
            return Convert.ToHexStringLower(SHA256.HashData(bytes));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    private static void Append(StringBuilder target, string value)
    {
        target.Append(value.Length.ToString(CultureInfo.InvariantCulture));
        target.Append(':');
        target.Append(value);
    }
}

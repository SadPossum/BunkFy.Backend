namespace BunkFy.Modules.DataRights.Application.Models;

using System.Globalization;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Models;

public sealed record TenantTerminationExportFragmentSetCoordinates(
    string TenantId,
    Guid ProcessId,
    Guid CaseId,
    long ApprovalRevision,
    long ExportOperationRevision,
    Guid TerminationEpoch,
    string PolicyEvidenceSha256);

public sealed record TenantTerminationExportFragmentManifestEntry(
    string OwnerKey,
    Guid FragmentId,
    long FragmentVersion,
    Guid FragmentIdempotencyKey,
    long FreezeOperationRevision,
    long ExportOperationRevision,
    int OwnerContractVersion,
    int CatalogVersion,
    string CatalogSha256,
    string FrozenRevisionSha256,
    long RecordCount,
    long SelectedProofRevision,
    long ResultingProofRevision,
    string ResultCode,
    string StorageKey,
    long EncryptedByteLength,
    string PlaintextSha256,
    int EncryptionKeyVersion,
    int FormatVersion,
    Guid GenerationRunId,
    int GenerationAttempt,
    DateTimeOffset AvailableAtUtc,
    DateTimeOffset ExpiresAtUtc)
{
    public static bool TryCreate(
        TenantTerminationExportFragment fragment,
        [NotNullWhen(true)]
        out TenantTerminationExportFragmentManifestEntry? entry)
    {
        ArgumentNullException.ThrowIfNull(fragment);
        if (fragment.State != TenantTerminationExportFragmentState.Available ||
            fragment.RecordCount is not long recordCount ||
            fragment.SelectedProofRevision is not long selectedProofRevision ||
            fragment.ResultingProofRevision is not long resultingProofRevision ||
            string.IsNullOrWhiteSpace(fragment.ResultCode) ||
            string.IsNullOrWhiteSpace(fragment.StorageKey) ||
            fragment.EncryptedByteLength is not long encryptedByteLength ||
            string.IsNullOrWhiteSpace(fragment.PlaintextSha256) ||
            fragment.EncryptionKeyVersion is not int encryptionKeyVersion ||
            fragment.FormatVersion is not int formatVersion ||
            fragment.GenerationRunId is not Guid generationRunId ||
            fragment.GenerationAttempt is not int generationAttempt ||
            fragment.AvailableAtUtc is not DateTimeOffset availableAtUtc)
        {
            entry = null;
            return false;
        }

        entry = new(
            fragment.OwnerKey,
            fragment.Id,
            fragment.Version,
            fragment.IdempotencyKey,
            fragment.FreezeOperationRevision,
            fragment.ExportOperationRevision,
            fragment.OwnerContractVersion,
            fragment.CatalogVersion,
            fragment.CatalogSha256,
            fragment.FrozenRevisionSha256,
            recordCount,
            selectedProofRevision,
            resultingProofRevision,
            fragment.ResultCode,
            fragment.StorageKey,
            encryptedByteLength,
            fragment.PlaintextSha256,
            encryptionKeyVersion,
            formatVersion,
            generationRunId,
            generationAttempt,
            availableAtUtc,
            fragment.ExpiresAtUtc);
        return true;
    }
}

public static class TenantTerminationExportFragmentSet
{
    private const string DigestDomain =
        "bunkfy.tenant-termination.export-fragment-set.v1";

    public static string ComputeSha256(
        TenantTerminationExportFragmentSetCoordinates coordinates,
        IReadOnlyCollection<TenantTerminationExportFragmentManifestEntry> fragments)
    {
        ArgumentNullException.ThrowIfNull(coordinates);
        ArgumentNullException.ThrowIfNull(fragments);

        StringBuilder canonical = new();
        Append(canonical, DigestDomain);
        Append(canonical, coordinates.TenantId);
        Append(canonical, coordinates.ProcessId.ToString("N"));
        Append(canonical, coordinates.CaseId.ToString("N"));
        Append(canonical, coordinates.ApprovalRevision.ToString(
            CultureInfo.InvariantCulture));
        Append(canonical, coordinates.ExportOperationRevision.ToString(
            CultureInfo.InvariantCulture));
        Append(canonical, coordinates.TerminationEpoch.ToString("N"));
        Append(canonical, coordinates.PolicyEvidenceSha256);
        foreach (TenantTerminationExportFragmentManifestEntry fragment in
                 fragments
                     .OrderBy(item => item.OwnerKey, StringComparer.Ordinal)
                     .ThenBy(item => item.FragmentId))
        {
            Append(canonical, fragment.OwnerKey);
            Append(canonical, fragment.FragmentId.ToString("N"));
            Append(canonical, fragment.FragmentVersion.ToString(
                CultureInfo.InvariantCulture));
            Append(canonical, fragment.FragmentIdempotencyKey.ToString("N"));
            Append(canonical, fragment.FreezeOperationRevision.ToString(
                CultureInfo.InvariantCulture));
            Append(canonical, fragment.ExportOperationRevision.ToString(
                CultureInfo.InvariantCulture));
            Append(canonical, fragment.OwnerContractVersion.ToString(
                CultureInfo.InvariantCulture));
            Append(canonical, fragment.CatalogVersion.ToString(
                CultureInfo.InvariantCulture));
            Append(canonical, fragment.CatalogSha256);
            Append(canonical, fragment.FrozenRevisionSha256);
            Append(canonical, fragment.RecordCount.ToString(
                CultureInfo.InvariantCulture));
            Append(canonical, fragment.SelectedProofRevision.ToString(
                CultureInfo.InvariantCulture));
            Append(canonical, fragment.ResultingProofRevision.ToString(
                CultureInfo.InvariantCulture));
            Append(canonical, fragment.ResultCode);
            Append(canonical, fragment.StorageKey);
            Append(canonical, fragment.EncryptedByteLength.ToString(
                CultureInfo.InvariantCulture));
            Append(canonical, fragment.PlaintextSha256);
            Append(canonical, fragment.EncryptionKeyVersion.ToString(
                CultureInfo.InvariantCulture));
            Append(canonical, fragment.FormatVersion.ToString(
                CultureInfo.InvariantCulture));
            Append(canonical, fragment.GenerationRunId.ToString("N"));
            Append(canonical, fragment.GenerationAttempt.ToString(
                CultureInfo.InvariantCulture));
            Append(canonical, fragment.AvailableAtUtc.ToUniversalTime()
                .ToString("O", CultureInfo.InvariantCulture));
            Append(canonical, fragment.ExpiresAtUtc.ToUniversalTime()
                .ToString("O", CultureInfo.InvariantCulture));
        }

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

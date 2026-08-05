namespace BunkFy.Modules.DataRights.Persistence;

using System.Security.Cryptography;
using System.Text;
using Gma.Framework.FileManagement;

internal static class DataRightsExportStorageKey
{
    public static FileStorageObjectKey Create(
        string tenantId,
        Guid artifactId)
    {
        string normalizedTenant = tenantId?.Trim().ToLowerInvariant() ??
            string.Empty;
        if (normalizedTenant.Length == 0 || artifactId == Guid.Empty)
        {
            throw new ArgumentException(
                "A tenant and artifact id are required for export storage.");
        }

        byte[] tenant = Encoding.UTF8.GetBytes(normalizedTenant);
        byte[] digest = SHA256.HashData(tenant);
        try
        {
            string scopeSegment =
                Convert.ToHexStringLower(digest.AsSpan(0, 8));
            return new FileStorageObjectKey(
                $"data-rights/exports/scope-{scopeSegment}/" +
                $"artifact-{artifactId:N}.bfdrx");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(tenant);
            CryptographicOperations.ZeroMemory(digest);
        }
    }

    public static FileStorageObjectKey CreateTenantTerminationFragment(
        string tenantId,
        Guid processId,
        Guid fragmentId)
    {
        if (processId == Guid.Empty || fragmentId == Guid.Empty)
        {
            throw new ArgumentException(
                "A process and fragment id are required for tenant export storage.");
        }

        string scopeSegment = ScopeSegment(tenantId);
        return new FileStorageObjectKey(
            $"data-rights/tenant-exports/scope-{scopeSegment}/" +
            $"process-{processId:N}/fragment-{fragmentId:N}.bftxf");
    }

    public static FileStorageObjectKey CreateTenantTerminationArtifact(
        string tenantId,
        Guid processId,
        Guid artifactId)
    {
        if (processId == Guid.Empty || artifactId == Guid.Empty)
        {
            throw new ArgumentException(
                "A process and artifact id are required for tenant export storage.");
        }

        string scopeSegment = ScopeSegment(tenantId);
        return new FileStorageObjectKey(
            $"data-rights/tenant-exports/scope-{scopeSegment}/" +
            $"process-{processId:N}/artifact-{artifactId:N}.bftxa");
    }

    private static string ScopeSegment(string tenantId)
    {
        string normalizedTenant = tenantId?.Trim().ToLowerInvariant() ??
            string.Empty;
        if (normalizedTenant.Length == 0)
        {
            throw new ArgumentException(
                "A tenant is required for export storage.",
                nameof(tenantId));
        }

        byte[] tenant = Encoding.UTF8.GetBytes(normalizedTenant);
        byte[] digest = SHA256.HashData(tenant);
        try
        {
            return Convert.ToHexStringLower(digest.AsSpan(0, 8));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(tenant);
            CryptographicOperations.ZeroMemory(digest);
        }
    }
}

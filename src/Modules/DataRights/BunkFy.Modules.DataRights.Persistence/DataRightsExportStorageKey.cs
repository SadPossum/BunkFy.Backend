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
}

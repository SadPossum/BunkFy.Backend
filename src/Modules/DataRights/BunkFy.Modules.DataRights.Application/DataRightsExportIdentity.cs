namespace BunkFy.Modules.DataRights.Application;

using System.Security.Cryptography;
using System.Text;
using BunkFy.Modules.DataRights.Contracts;

internal static class DataRightsExportIdentity
{
    public static string SelectionSha256(
        IEnumerable<DataRightsSubjectCoordinate> subjects)
    {
        ArgumentNullException.ThrowIfNull(subjects);

        StringBuilder canonical = new();
        foreach (DataRightsSubjectCoordinate subject in subjects
            .OrderBy(item => item.OwnerKey, StringComparer.Ordinal)
            .ThenBy(item => item.RecordType, StringComparer.Ordinal)
            .ThenBy(item => item.RecordId))
        {
            canonical
                .Append(subject.OwnerKey.Trim().ToLowerInvariant()).Append('\0')
                .Append(subject.RecordType.Trim().ToLowerInvariant()).Append('\0')
                .Append(subject.RecordId.ToString("N")).Append('\0')
                .Append(subject.RecordVersion).Append('\n');
        }

        byte[] bytes = Encoding.UTF8.GetBytes(canonical.ToString());
        try
        {
            return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }
}

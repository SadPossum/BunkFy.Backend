namespace BunkFy.Modules.Staff.Persistence.TenantTermination;

using System.Security.Cryptography;
using System.Text;

internal static class StaffTenantLifecycleHashes
{
    public static string Sha256(string value) =>
        Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    public static bool IsSha256(string? value) =>
        value is { Length: 64 } && value.All(character =>
            character is (>= '0' and <= '9') or (>= 'a' and <= 'f'));
}

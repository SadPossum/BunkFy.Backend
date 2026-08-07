namespace BunkFy.Modules.Staff.Application.Handlers;

using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using BunkFy.Modules.Staff.Domain.ValueObjects;

internal static class StaffProfileUpdateFingerprint
{
    public static string Compute(
        Guid staffMemberId,
        long expectedVersion,
        StaffProfile profile) => Hash(
        "staff-profile-update-v1",
        staffMemberId.ToString("N"),
        expectedVersion.ToString(CultureInfo.InvariantCulture),
        profile.DisplayName,
        profile.LegalName,
        profile.WorkEmail,
        profile.WorkPhone,
        profile.EmployeeNumber,
        profile.JobTitle,
        profile.Department);

    private static string Hash(params string?[] values)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(
            HashAlgorithmName.SHA256);
        Span<byte> length = stackalloc byte[sizeof(int)];
        foreach (string? value in values)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            BinaryPrimitives.WriteInt32BigEndian(length, bytes.Length);
            hash.AppendData(length);
            hash.AppendData(bytes);
        }

        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }
}

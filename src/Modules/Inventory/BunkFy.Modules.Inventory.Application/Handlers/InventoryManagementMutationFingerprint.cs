namespace BunkFy.Modules.Inventory.Application.Handlers;

using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using BunkFy.Modules.Inventory.Contracts;

internal static class InventoryManagementMutationFingerprint
{
    public static string ComputeRoomSalesMode(
        Guid propertyId,
        Guid roomId,
        long expectedVersion,
        InventorySalesMode salesMode) => Compute(
            "bunkfy-inventory-room-sales-mode/v1",
            propertyId.ToString("N"),
            roomId.ToString("N"),
            expectedVersion.ToString(CultureInfo.InvariantCulture),
            ((int)salesMode).ToString(CultureInfo.InvariantCulture));

    private static string Compute(params string[] values)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(
            HashAlgorithmName.SHA256);
        Span<byte> length = stackalloc byte[sizeof(int)];
        foreach (string value in values)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            BinaryPrimitives.WriteInt32BigEndian(length, bytes.Length);
            hash.AppendData(length);
            hash.AppendData(bytes);
        }

        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }
}

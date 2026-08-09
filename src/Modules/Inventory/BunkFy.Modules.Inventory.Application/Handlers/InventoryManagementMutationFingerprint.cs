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

    public static string ComputeManualBlockCreate(
        Guid propertyId,
        InventoryBlockTarget target,
        DateOnly arrival,
        DateOnly departure,
        string reason,
        bool group) => Compute(
            group
                ? "bunkfy-inventory-manual-block-group-create/v1"
                : "bunkfy-inventory-manual-block-create/v1",
            propertyId.ToString("N"),
            ((int)target.Kind).ToString(CultureInfo.InvariantCulture),
            target.BuildingLabel ?? string.Empty,
            target.FloorLabel ?? string.Empty,
            target.RoomId?.ToString("N") ?? string.Empty,
            target.InventoryUnitId?.ToString("N") ?? string.Empty,
            arrival.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            departure.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            NormalizeReason(reason));

    public static string ComputeManualBlockRelease(
        Guid propertyId,
        Guid blockId,
        long expectedVersion) => Compute(
            "bunkfy-inventory-manual-block-release/v1",
            propertyId.ToString("N"),
            blockId.ToString("N"),
            expectedVersion.ToString(CultureInfo.InvariantCulture));

    public static string ComputeManualBlockGroupRelease(
        Guid propertyId,
        Guid blockGroupId) => Compute(
            "bunkfy-inventory-manual-block-group-release/v1",
            propertyId.ToString("N"),
            blockGroupId.ToString("N"));

    public static string ComputeBedRetirementRequest(
        Guid propertyId,
        Guid roomId,
        Guid bedId,
        string reason) => Compute(
            "bunkfy-inventory-bed-retirement-request/v1",
            propertyId.ToString("N"),
            roomId.ToString("N"),
            bedId.ToString("N"),
            NormalizeReason(reason));

    public static string ComputeBedRetirementRetry(
        Guid propertyId,
        Guid topologyChangeId,
        long expectedVersion) => Compute(
            "bunkfy-inventory-bed-retirement-retry/v1",
            propertyId.ToString("N"),
            topologyChangeId.ToString("N"),
            expectedVersion.ToString(CultureInfo.InvariantCulture));

    public static string ComputeRoomRetirementRequest(
        Guid propertyId,
        Guid roomId,
        string reason) => Compute(
            "bunkfy-inventory-room-retirement-request/v1",
            propertyId.ToString("N"),
            roomId.ToString("N"),
            NormalizeReason(reason));

    public static string ComputeRoomRetirementRetry(
        Guid propertyId,
        Guid topologyChangeId,
        long expectedVersion) => Compute(
            "bunkfy-inventory-room-retirement-retry/v1",
            propertyId.ToString("N"),
            topologyChangeId.ToString("N"),
            expectedVersion.ToString(CultureInfo.InvariantCulture));

    public static string NormalizeReason(string? reason) =>
        reason?.Trim() ?? string.Empty;

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

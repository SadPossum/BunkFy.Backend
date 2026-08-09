namespace BunkFy.Modules.Inventory.Application.Handlers;

using BunkFy.Modules.Inventory.Contracts;

internal static class InventoryBlockTargetNormalizer
{
    public static bool TryNormalize(
        InventoryBlockTarget? target,
        out InventoryBlockTarget normalized)
    {
        normalized = target?.Kind switch
        {
            InventoryBlockTargetKind.Property => new(
                InventoryBlockTargetKind.Property),
            InventoryBlockTargetKind.Building when
                Normalize(target.BuildingLabel) is { } building => new(
                    InventoryBlockTargetKind.Building,
                    BuildingLabel: building),
            InventoryBlockTargetKind.Floor when
                Normalize(target.FloorLabel) is { } floor => new(
                    InventoryBlockTargetKind.Floor,
                    BuildingLabel: Normalize(target.BuildingLabel),
                    FloorLabel: floor),
            InventoryBlockTargetKind.Room when
                target.RoomId is { } roomId && roomId != Guid.Empty => new(
                    InventoryBlockTargetKind.Room,
                    RoomId: roomId),
            InventoryBlockTargetKind.Unit when
                target.InventoryUnitId is { } unitId &&
                unitId != Guid.Empty => new(
                    InventoryBlockTargetKind.Unit,
                    InventoryUnitId: unitId),
            _ => null!
        };
        return normalized is not null;
    }

    private static string? Normalize(string? value)
    {
        string normalized = value?.Trim() ?? string.Empty;
        return normalized.Length == 0 ? null : normalized;
    }
}

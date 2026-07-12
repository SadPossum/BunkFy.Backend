namespace BunkFy.Modules.Inventory.Application.Handlers;

using BunkFy.Modules.Inventory.Contracts;
using BunkFy.Modules.Inventory.Domain.Aggregates;

internal static class ManualInventoryBlockMappings
{
    public static ManualInventoryBlockDto ToDto(this ManualInventoryBlock block) => new(
        block.Id,
        block.PropertyId,
        block.InventoryUnitId,
        block.Arrival,
        block.Departure,
        block.Reason,
        block.Status == ManualInventoryBlockState.Active
            ? ManualInventoryBlockStatus.Active
            : ManualInventoryBlockStatus.Released,
        block.Version,
        block.CreatedAtUtc,
        block.ReleasedAtUtc);
}

namespace BunkFy.Modules.Inventory.Application.Handlers;

using BunkFy.Modules.Inventory.Contracts;
using BunkFy.Modules.Inventory.Domain.Aggregates;

internal static class ManualInventoryBlockMappings
{
    public static ManualInventoryBlockMutationReceiptDto ToMutationReceipt(this ManualInventoryBlock block) => new(
        block.Id,
        block.BlockGroupId,
        block.PropertyId,
        block.Status == ManualInventoryBlockState.Active
            ? ManualInventoryBlockStatus.Active
            : ManualInventoryBlockStatus.Released,
        block.Version);

    public static ManualInventoryBlockGroupMutationReceiptDto ToMutationReceipt(
        this ManualInventoryBlockCreationResult result) => new(
        result.BlockGroupId,
        result.Blocks.First().PropertyId,
        result.Blocks.Count);
}

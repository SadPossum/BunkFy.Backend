namespace BunkFy.Modules.Inventory.Contracts;

public sealed record ManualInventoryBlockGroupDto(
    Guid BlockGroupId,
    IReadOnlyCollection<ManualInventoryBlockDto> Blocks);

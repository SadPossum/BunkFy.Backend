namespace BunkFy.Modules.Inventory.Contracts;

public sealed record ManualInventoryBlockMutationReceiptDto(
    Guid BlockId,
    Guid BlockGroupId,
    Guid PropertyId,
    ManualInventoryBlockStatus Status,
    long Version);

public sealed record ManualInventoryBlockGroupMutationReceiptDto(
    Guid BlockGroupId,
    Guid PropertyId,
    int AffectedBlockCount);

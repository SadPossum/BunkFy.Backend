namespace BunkFy.Modules.Inventory.Contracts;

public sealed record ManualInventoryBlockListResponse(
    IReadOnlyCollection<ManualInventoryBlockDto> Blocks,
    int Page,
    int PageSize);

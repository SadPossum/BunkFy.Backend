namespace Inventory.Application.Queries;

using Gma.Framework.Cqrs;
using Inventory.Contracts;

public sealed record ListManualInventoryBlocksQuery(
    Guid PropertyId,
    Guid? InventoryUnitId,
    bool IncludeReleased,
    int Page,
    int PageSize)
    : IQuery<ManualInventoryBlockListResponse>;

namespace Inventory.Application.Commands;

using Gma.Framework.Cqrs;
using Inventory.Contracts;

public sealed record ConfigureRoomSalesModeCommand(
    Guid PropertyId,
    Guid RoomId,
    InventorySalesMode SalesMode,
    long ExpectedVersion)
    : ITransactionalCommand<RoomInventoryDto>;

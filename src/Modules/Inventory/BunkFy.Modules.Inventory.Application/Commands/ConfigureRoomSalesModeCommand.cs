namespace BunkFy.Modules.Inventory.Application.Commands;

using BunkFy.Modules.Inventory.Contracts;
using Gma.Framework.Cqrs;

public sealed record ConfigureRoomSalesModeCommand(
    Guid OperationId,
    Guid PropertyId,
    Guid RoomId,
    InventorySalesMode SalesMode,
    long ExpectedVersion,
    string? ActorId = null)
    : ITransactionalCommand<RoomInventoryMutationReceiptDto>;

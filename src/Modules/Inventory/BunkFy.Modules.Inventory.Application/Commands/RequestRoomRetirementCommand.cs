namespace BunkFy.Modules.Inventory.Application.Commands;

using BunkFy.Modules.Inventory.Contracts;
using Gma.Framework.Cqrs;

public sealed record RequestRoomRetirementCommand(
    Guid OperationId,
    Guid PropertyId,
    Guid RoomId,
    bool Confirmed,
    string Reason,
    string RequestedBy)
    : ITransactionalCommand<RoomRetirementDto>;

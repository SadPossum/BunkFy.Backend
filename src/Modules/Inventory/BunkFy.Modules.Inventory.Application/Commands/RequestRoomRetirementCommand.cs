namespace BunkFy.Modules.Inventory.Application.Commands;

using BunkFy.Modules.Inventory.Contracts;
using Gma.Framework.Cqrs;

public sealed record RequestRoomRetirementCommand(
    Guid PropertyId,
    Guid RoomId,
    string Reason,
    string RequestedBy)
    : ITransactionalCommand<RoomRetirementDto>;

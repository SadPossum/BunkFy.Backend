namespace Properties.Application.Commands;

using Properties.Contracts;
using Gma.Framework.Cqrs;

public sealed record UpdateRoomCommand(
    Guid RoomId,
    string Name,
    string? BuildingLabel,
    string? FloorLabel)
    : ITransactionalCommand<RoomDto>;

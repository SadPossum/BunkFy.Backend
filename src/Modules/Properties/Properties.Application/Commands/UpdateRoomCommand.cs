namespace Properties.Application.Commands;

using Properties.Contracts;
using Gma.Framework.Cqrs;

public sealed record UpdateRoomCommand(
    Guid PropertyId,
    Guid RoomId,
    long ExpectedVersion,
    string Name,
    string? BuildingLabel,
    string? FloorLabel)
    : ITransactionalCommand<RoomDto>;

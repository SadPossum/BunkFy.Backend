namespace BunkFy.Modules.Properties.Application.Commands;

using BunkFy.Modules.Properties.Contracts;
using Gma.Framework.Cqrs;

public sealed record CreateRoomCommand(
    Guid PropertyId,
    long ExpectedPropertyVersion,
    string Name,
    string? BuildingLabel,
    string? FloorLabel)
    : ITransactionalCommand<RoomDto>;

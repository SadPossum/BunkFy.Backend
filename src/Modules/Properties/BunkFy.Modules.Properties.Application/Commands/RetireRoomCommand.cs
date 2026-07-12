namespace BunkFy.Modules.Properties.Application.Commands;

using Gma.Framework.Cqrs;

public sealed record RetireRoomCommand(
    Guid PropertyId,
    Guid RoomId,
    long ExpectedVersion,
    bool CascadeBeds) : ITransactionalCommand<Unit>;

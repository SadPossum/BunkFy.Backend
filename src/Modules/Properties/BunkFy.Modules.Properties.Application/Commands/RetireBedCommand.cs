namespace BunkFy.Modules.Properties.Application.Commands;

using Gma.Framework.Cqrs;

public sealed record RetireBedCommand(
    Guid PropertyId,
    Guid RoomId,
    Guid BedId,
    long ExpectedRoomVersion) : ITransactionalCommand<Unit>;

namespace BunkFy.Modules.Properties.Application.Commands;

using BunkFy.Modules.Properties.Contracts;
using Gma.Framework.Cqrs;

public sealed record UpdateBedCommand(
    Guid PropertyId,
    Guid RoomId,
    Guid BedId,
    long ExpectedRoomVersion,
    string Label) : ITransactionalCommand<BedDto>;

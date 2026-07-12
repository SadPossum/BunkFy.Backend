namespace BunkFy.Modules.Properties.Application.Commands;

using BunkFy.Modules.Properties.Contracts;
using Gma.Framework.Cqrs;

public sealed record AddBedCommand(
    Guid PropertyId,
    Guid RoomId,
    long ExpectedRoomVersion,
    string Label) : ITransactionalCommand<BedDto>;

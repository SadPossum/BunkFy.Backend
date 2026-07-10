namespace Properties.Application.Commands;

using Properties.Contracts;
using Gma.Framework.Cqrs;

public sealed record UpdateBedCommand(
    Guid PropertyId,
    Guid RoomId,
    Guid BedId,
    long ExpectedRoomVersion,
    string Label) : ITransactionalCommand<BedDto>;

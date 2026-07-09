namespace Properties.Application.Commands;

using Properties.Contracts;
using Gma.Framework.Cqrs;

public sealed record UpdateBedCommand(Guid RoomId, Guid BedId, string Label) : ITransactionalCommand<BedDto>;

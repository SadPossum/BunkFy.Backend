namespace Properties.Application.Commands;

using Properties.Contracts;
using Gma.Framework.Cqrs;

public sealed record AddBedCommand(Guid RoomId, string Label) : ITransactionalCommand<BedDto>;

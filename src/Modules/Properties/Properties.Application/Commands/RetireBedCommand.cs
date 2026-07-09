namespace Properties.Application.Commands;

using Gma.Framework.Cqrs;

public sealed record RetireBedCommand(Guid RoomId, Guid BedId) : ITransactionalCommand<Unit>;

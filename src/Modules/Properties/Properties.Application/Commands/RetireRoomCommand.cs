namespace Properties.Application.Commands;

using Gma.Framework.Cqrs;

public sealed record RetireRoomCommand(Guid RoomId) : ITransactionalCommand<Unit>;

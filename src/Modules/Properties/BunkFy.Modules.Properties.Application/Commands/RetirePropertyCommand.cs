namespace BunkFy.Modules.Properties.Application.Commands;

using Gma.Framework.Cqrs;

public sealed record RetirePropertyCommand(Guid PropertyId, long ExpectedVersion) : ITransactionalCommand<Unit>;

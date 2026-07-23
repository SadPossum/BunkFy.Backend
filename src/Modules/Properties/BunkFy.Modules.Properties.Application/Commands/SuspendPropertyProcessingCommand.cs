namespace BunkFy.Modules.Properties.Application.Commands;

using Gma.Framework.Cqrs;
using Gma.Framework.Results;

public sealed record SuspendPropertyProcessingCommand(
    Guid PropertyId,
    long ExpectedVersion,
    string ActorId)
    : ITransactionalCommand<Unit>;

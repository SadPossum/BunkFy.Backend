namespace BunkFy.Modules.Properties.Application.Commands;

using BunkFy.Modules.Properties.Contracts;
using Gma.Framework.Cqrs;

public sealed record SuspendPropertyProcessingCommand(
    Guid PropertyId,
    Guid OperationId,
    bool Confirmed,
    long ExpectedVersion,
    string ActorId)
    : ITransactionalCommand<PropertyMutationReceiptDto>;

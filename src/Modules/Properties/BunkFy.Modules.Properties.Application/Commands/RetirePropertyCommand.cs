namespace BunkFy.Modules.Properties.Application.Commands;

using BunkFy.Modules.Properties.Contracts;
using Gma.Framework.Cqrs;

public sealed record RetirePropertyCommand(
    Guid PropertyId,
    Guid OperationId,
    bool Confirmed,
    long ExpectedVersion,
    string? ActorId = null)
    : ITransactionalCommand<PropertyMutationReceiptDto>;

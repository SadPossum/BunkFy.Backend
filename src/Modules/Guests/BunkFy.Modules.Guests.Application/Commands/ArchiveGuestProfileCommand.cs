namespace BunkFy.Modules.Guests.Application.Commands;

using BunkFy.Modules.Guests.Application.Ports;
using Gma.Framework.Cqrs;
using BunkFy.Modules.Guests.Contracts;

public sealed record ArchiveGuestProfileCommand(
    Guid OperationId,
    Guid PropertyId,
    Guid GuestId,
    long ExpectedVersion,
    string ActorId) :
    ITransactionalCommand<GuestMutationReceiptDto>,
    IGuestsPersistenceRetryableCommand;

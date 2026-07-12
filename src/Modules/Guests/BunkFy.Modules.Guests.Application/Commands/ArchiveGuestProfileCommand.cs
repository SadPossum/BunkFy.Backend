namespace BunkFy.Modules.Guests.Application.Commands;

using Gma.Framework.Cqrs;
using BunkFy.Modules.Guests.Contracts;

public sealed record ArchiveGuestProfileCommand(
    Guid PropertyId,
    Guid GuestId,
    long ExpectedVersion,
    string ActorId) : ITransactionalCommand<GuestProfileDto>;

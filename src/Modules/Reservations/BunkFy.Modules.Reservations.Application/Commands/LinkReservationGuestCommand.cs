namespace BunkFy.Modules.Reservations.Application.Commands;

using Gma.Framework.Cqrs;
using BunkFy.Modules.Reservations.Contracts;

public sealed record LinkReservationGuestCommand(
    Guid PropertyId,
    Guid ReservationId,
    Guid GuestId,
    ReservationGuestRoleKind Role,
    bool ReplaceExistingRole,
    long ExpectedVersion,
    string ActorId) : ITransactionalCommand<ReservationDto>;

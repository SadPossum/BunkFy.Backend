namespace BunkFy.Modules.Reservations.Application.Commands;

using Gma.Framework.Cqrs;
using BunkFy.Modules.Reservations.Contracts;

public sealed record UpdateReservationGuestDetailsCommand(
    Guid PropertyId,
    Guid ReservationId,
    string PrimaryGuestName,
    string? Email,
    string? Phone,
    int GuestCount,
    string? Notes,
    long ExpectedDetailsRevision,
    ReservationDetailsChangeOriginKind Origin,
    string ActorId)
    : ITransactionalCommand<ReservationDto>;

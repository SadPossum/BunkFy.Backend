namespace BunkFy.Modules.Reservations.Application.Commands;

using BunkFy.Modules.Reservations.Contracts;
using Gma.Framework.Cqrs;

public sealed record PrepareReservationGuestRecordLinkCommand(
    Guid OperationId,
    Guid PropertyId,
    Guid ReservationId,
    long ExpectedReservationVersion,
    string ActorId) :
    ITransactionalCommand<ReservationGuestRecordLinkPreparationDto>;

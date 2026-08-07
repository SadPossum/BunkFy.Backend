namespace BunkFy.Modules.Reservations.Application.Commands;

using BunkFy.Modules.Reservations.Contracts;
using Gma.Framework.Cqrs;

public sealed record RetryReservationGuestRecordLinkCommand(
    Guid OperationId,
    Guid PropertyId,
    Guid ReservationId) :
    ITransactionalCommand<ReservationGuestRecordLinkProcessDto>;

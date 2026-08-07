namespace BunkFy.Modules.Reservations.Application.Commands;

using BunkFy.Modules.Reservations.Contracts;
using Gma.Framework.Cqrs;

public sealed record ConfirmReservationGuestRecordLinkCommand(
    Guid OperationId,
    Guid PropertyId,
    Guid ReservationId,
    Guid CreationConfirmationId) :
    ITransactionalCommand<ReservationGuestRecordLinkProcessDto>;

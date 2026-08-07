namespace BunkFy.Modules.Reservations.Application.Commands;

using BunkFy.Modules.Reservations.Contracts;
using Gma.Framework.Cqrs;

public sealed record AdvanceReservationGuestRecordLinkCommand(
    Guid OperationId,
    Guid PropertyId,
    Guid ReservationId,
    int DispatchRevision,
    bool IsFinalAttempt) :
    ITransactionalCommand<ReservationGuestRecordLinkProcessDto>;

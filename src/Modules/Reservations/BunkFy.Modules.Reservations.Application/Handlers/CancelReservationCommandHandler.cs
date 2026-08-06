namespace BunkFy.Modules.Reservations.Application.Handlers;

using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using BunkFy.Modules.Reservations.Application.Commands;
using BunkFy.Modules.Reservations.Application.Ports;
using BunkFy.Modules.Reservations.Contracts;

internal sealed class CancelReservationCommandHandler(
    ReservationManagementLifecycleCoordinator lifecycle,
    IIdGenerator idGenerator)
    : ICommandHandler<CancelReservationCommand, ReservationMutationReceiptDto>
{
    public async Task<Result<ReservationMutationReceiptDto>> HandleAsync(
        CancelReservationCommand command,
        CancellationToken cancellationToken)
    {
        return await lifecycle.ExecuteAsync(
            command.OperationId,
            command.PropertyId,
            command.ReservationId,
            ReservationManagementOperationKind.Cancel,
            command.ExpectedVersion,
            businessDate: null,
            command.ActorId,
            (reservation, nowUtc) => reservation.RequestCancellation(
                command.ExpectedVersion,
                idGenerator.NewId(),
                idGenerator.NewId(),
                nowUtc,
                command.ActorId),
            cancellationToken).ConfigureAwait(false);
    }
}

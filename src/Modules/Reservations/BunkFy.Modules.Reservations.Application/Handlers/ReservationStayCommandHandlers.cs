namespace BunkFy.Modules.Reservations.Application.Handlers;

using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using BunkFy.Modules.Reservations.Application.Commands;
using BunkFy.Modules.Reservations.Application.Ports;
using BunkFy.Modules.Reservations.Contracts;

internal sealed class CheckInReservationCommandHandler(
    ReservationManagementLifecycleCoordinator lifecycle,
    IIdGenerator ids)
    : ICommandHandler<CheckInReservationCommand, ReservationMutationReceiptDto>
{
    public async Task<Result<ReservationMutationReceiptDto>> HandleAsync(
        CheckInReservationCommand command,
        CancellationToken cancellationToken)
    {
        return await lifecycle.ExecuteAsync(
            command.OperationId,
            command.PropertyId,
            command.ReservationId,
            ReservationManagementOperationKind.CheckIn,
            command.ExpectedVersion,
            command.BusinessDate,
            command.ActorId,
            (reservation, nowUtc) => reservation.CheckIn(
                command.ExpectedVersion,
                command.BusinessDate,
                command.ActorId,
                ids.NewId(),
                nowUtc),
            cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class MarkReservationNoShowCommandHandler(
    ReservationManagementLifecycleCoordinator lifecycle,
    IIdGenerator ids)
    : ICommandHandler<MarkReservationNoShowCommand, ReservationMutationReceiptDto>
{
    public async Task<Result<ReservationMutationReceiptDto>> HandleAsync(
        MarkReservationNoShowCommand command,
        CancellationToken cancellationToken)
    {
        return await lifecycle.ExecuteAsync(
            command.OperationId,
            command.PropertyId,
            command.ReservationId,
            ReservationManagementOperationKind.NoShow,
            command.ExpectedVersion,
            command.BusinessDate,
            command.ActorId,
            (reservation, nowUtc) => reservation.RequestNoShow(
                command.ExpectedVersion,
                command.BusinessDate,
                command.ActorId,
                ids.NewId(),
                ids.NewId(),
                nowUtc),
            cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class CheckOutReservationCommandHandler(
    ReservationManagementLifecycleCoordinator lifecycle,
    IIdGenerator ids)
    : ICommandHandler<CheckOutReservationCommand, ReservationMutationReceiptDto>
{
    public async Task<Result<ReservationMutationReceiptDto>> HandleAsync(
        CheckOutReservationCommand command,
        CancellationToken cancellationToken)
    {
        return await lifecycle.ExecuteAsync(
            command.OperationId,
            command.PropertyId,
            command.ReservationId,
            ReservationManagementOperationKind.CheckOut,
            command.ExpectedVersion,
            command.BusinessDate,
            command.ActorId,
            (reservation, nowUtc) => reservation.RequestCheckout(
                command.ExpectedVersion,
                command.BusinessDate,
                command.ActorId,
                ids.NewId(),
                ids.NewId(),
                nowUtc),
            cancellationToken).ConfigureAwait(false);
    }
}

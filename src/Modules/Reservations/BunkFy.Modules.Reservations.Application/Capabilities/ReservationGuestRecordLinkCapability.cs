namespace BunkFy.Modules.Reservations.Application.Capabilities;

using BunkFy.Modules.Reservations.Application.Commands;
using BunkFy.Modules.Reservations.Application.Queries;
using BunkFy.Modules.Reservations.Contracts;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;

internal sealed class ReservationGuestRecordLinkCapability(
    IRequestDispatcher dispatcher) : IReservationGuestRecordLinkCapability
{
    public Task<Result<ReservationGuestRecordLinkPreparationDto>> PrepareAsync(
        PrepareReservationGuestRecordLinkRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return dispatcher.SendAsync(
            new PrepareReservationGuestRecordLinkCommand(
                request.OperationId,
                request.PropertyId,
                request.ReservationId,
                request.ExpectedReservationVersion,
                request.ActorId),
            cancellationToken);
    }

    public Task<Result<ReservationGuestRecordLinkProcessDto>> ConfirmGuestAsync(
        ConfirmReservationGuestRecordLinkRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return dispatcher.SendAsync(
            new ConfirmReservationGuestRecordLinkCommand(
                request.OperationId,
                request.PropertyId,
                request.ReservationId,
                request.CreationConfirmationId),
            cancellationToken);
    }

    public Task<Result<ReservationGuestRecordLinkProcessDto>> RetryAsync(
        RetryReservationGuestRecordLinkRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return dispatcher.SendAsync(
            new RetryReservationGuestRecordLinkCommand(
                request.OperationId,
                request.PropertyId,
                request.ReservationId),
            cancellationToken);
    }

    public Task<Result<ReservationGuestRecordLinkProcessDto>> GetAsync(
        GetReservationGuestRecordLinkRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return dispatcher.QueryAsync(
            new GetReservationGuestRecordLinkQuery(
                request.OperationId,
                request.PropertyId,
                request.ReservationId),
            cancellationToken);
    }
}

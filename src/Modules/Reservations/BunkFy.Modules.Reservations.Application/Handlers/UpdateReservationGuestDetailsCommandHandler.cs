namespace BunkFy.Modules.Reservations.Application.Handlers;

using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using BunkFy.Modules.Reservations.Application.Commands;
using BunkFy.Modules.Reservations.Application.Ports;
using BunkFy.Modules.Reservations.Contracts;
using BunkFy.Modules.Reservations.Domain.Aggregates;

internal sealed class UpdateReservationGuestDetailsCommandHandler(
    IReservationRepository reservations,
    ISystemClock clock,
    IIdGenerator idGenerator)
    : ICommandHandler<UpdateReservationGuestDetailsCommand, ReservationDto>
{
    public async Task<Result<ReservationDto>> HandleAsync(
        UpdateReservationGuestDetailsCommand command,
        CancellationToken cancellationToken)
    {
        Reservation? reservation = await reservations.GetAsync(
            command.PropertyId,
            command.ReservationId,
            cancellationToken).ConfigureAwait(false);
        if (reservation is null)
        {
            return Result.Failure<ReservationDto>(ReservationsApplicationErrors.ReservationNotFound);
        }

        ReservationDetailsChangeOrigin origin = command.Origin switch
        {
            ReservationDetailsChangeOriginKind.Staff => ReservationDetailsChangeOrigin.Staff,
            ReservationDetailsChangeOriginKind.Admin => ReservationDetailsChangeOrigin.Admin,
            ReservationDetailsChangeOriginKind.System => ReservationDetailsChangeOrigin.System,
            _ => ReservationDetailsChangeOrigin.Unknown
        };
        if (origin == ReservationDetailsChangeOrigin.Unknown)
        {
            return Result.Failure<ReservationDto>(ReservationsApplicationErrors.DetailsChangeProvenanceInvalid);
        }

        Result<ReservationDetailsChangeOutcome> changed = reservation.UpdateGuestDetails(
            command.PrimaryGuestName,
            command.Email,
            command.Phone,
            command.GuestCount,
            command.Notes,
            command.ExpectedDetailsRevision,
            origin,
            command.ActorId,
            adapterConnectionId: null,
            externalOperationId: null,
            idGenerator.NewId(),
            idGenerator.NewId(),
            clock.UtcNow,
            command.ExpectedArrivalTime,
            command.ExpectedDepartureTime);
        return changed.IsFailure
            ? Result.Failure<ReservationDto>(changed.Error)
            : Result.Success(reservation.ToDto());
    }
}

namespace BunkFy.Modules.Reservations.Application.Handlers;

using BunkFy.DataGovernance;
using BunkFy.Modules.Guests.Contracts;
using BunkFy.Modules.Reservations.Application.Commands;
using BunkFy.Modules.Reservations.Application.Policies;
using BunkFy.Modules.Reservations.Application.Ports;
using BunkFy.Modules.Reservations.Contracts;
using BunkFy.Modules.Reservations.Domain.Aggregates;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;

internal sealed class LinkReservationGuestCommandHandler(
    IReservationRepository reservations,
    IReservationGuestProfileProjectionRepository guests,
    IGuestProcessingRestrictionGate restrictionGate,
    IReservationCountryPolicyAdmission countryPolicy,
    IScopeContext scopeContext,
    ISystemClock clock,
    IIdGenerator ids)
    : ICommandHandler<LinkReservationGuestCommand, ReservationDto>
{
    public async Task<Result<ReservationDto>> HandleAsync(
        LinkReservationGuestCommand command,
        CancellationToken cancellationToken)
    {
        CountryPolicyDecision policyDecision = await countryPolicy.EvaluateAsync(
            command.PropertyId,
            ReservationCountryPolicyAdmission.ReservationManagementPurpose,
            CountryPolicySurface.ApiWrite,
            ReservationCountryPolicyAdmission.AuthorizedOperatorProvenance,
            cancellationToken).ConfigureAwait(false);
        if (!policyDecision.IsAllowed)
        {
            return Result.Failure<ReservationDto>(
                ReservationsApplicationErrors.CountryPolicyDenied(policyDecision.Reason));
        }

        Reservation? reservation = await reservations.GetAsync(
            command.PropertyId,
            command.ReservationId,
            cancellationToken).ConfigureAwait(false);
        if (reservation is null)
        {
            return Result.Failure<ReservationDto>(ReservationsApplicationErrors.ReservationNotFound);
        }

        if (command.GuestId == Guid.Empty || command.Role is not ReservationGuestRoleKind.Primary)
        {
            return Result.Failure<ReservationDto>(ReservationsApplicationErrors.ReservationGuestLinkInvalid);
        }

        bool alreadyLinked = reservation.Guests.Any(guest => guest.IsCurrent &&
            guest.GuestId == command.GuestId && (int)guest.Role == (int)command.Role);
        if (!alreadyLinked)
        {
            if (!scopeContext.IsEnabled ||
                string.IsNullOrWhiteSpace(scopeContext.ScopeId) ||
                !await guests.IsLinkableAsync(
                    command.PropertyId,
                    command.GuestId,
                    cancellationToken).ConfigureAwait(false))
            {
                return Result.Failure<ReservationDto>(
                    ReservationsApplicationErrors.GuestNotLinkable);
            }

            GuestProcessingRestrictionGateResult restriction =
                await restrictionGate.EvaluateAsync(
                    new(
                        scopeContext.ScopeId,
                        command.PropertyId,
                        command.GuestId),
                    cancellationToken).ConfigureAwait(false);
            if (!restriction.IsAllowed)
            {
                return Result.Failure<ReservationDto>(
                    ReservationsApplicationErrors.GuestNotLinkable);
            }
        }

        Result<bool> linked = reservation.LinkGuest(
            command.GuestId,
            (ReservationGuestRole)(int)command.Role,
            command.ReplaceExistingRole,
            command.ExpectedVersion,
            command.ActorId,
            ids.NewId(),
            clock.UtcNow);
        return linked.IsSuccess
            ? Result.Success(reservation.ToDto())
            : Result.Failure<ReservationDto>(linked.Error);
    }
}

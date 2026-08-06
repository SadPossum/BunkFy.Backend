namespace BunkFy.Modules.Reservations.Application.Handlers;

using BunkFy.DataGovernance;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using BunkFy.Modules.Reservations.Application.Commands;
using BunkFy.Modules.Reservations.Application.Ports;
using BunkFy.Modules.Reservations.Application.Policies;
using BunkFy.Modules.Reservations.Contracts;
using BunkFy.Modules.Reservations.Domain.Aggregates;

internal sealed class UpdateReservationGuestDetailsCommandHandler(
    ReservationMutationCoordinator mutations,
    IReservationCountryPolicyAdmission countryPolicy,
    IReservationManagementOperationRepository operations,
    IReservationDetailsHistoryReader history,
    ISystemClock clock,
    IIdGenerator idGenerator)
    : ICommandHandler<UpdateReservationGuestDetailsCommand, ReservationMutationReceiptDto>
{
    public async Task<Result<ReservationMutationReceiptDto>> HandleAsync(
        UpdateReservationGuestDetailsCommand command,
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
            return Result.Failure<ReservationMutationReceiptDto>(
                ReservationsApplicationErrors.CountryPolicyDenied(policyDecision.Reason));
        }

        if (command.OperationId == Guid.Empty)
        {
            return Result.Failure<ReservationMutationReceiptDto>(
                ReservationsApplicationErrors.ManagementOperationInvalid);
        }

        Reservation? reservation = await mutations.AcquireOperationalAsync(
            command.PropertyId,
            command.ReservationId,
            cancellationToken).ConfigureAwait(false);
        if (reservation is null)
        {
            return Result.Failure<ReservationMutationReceiptDto>(ReservationsApplicationErrors.ReservationNotFound);
        }

        ReservationDetailsChangeOrigin origin = command.Origin switch
        {
            ReservationDetailsChangeOriginKind.Staff => ReservationDetailsChangeOrigin.Staff,
            ReservationDetailsChangeOriginKind.Admin => ReservationDetailsChangeOrigin.Admin,
            ReservationDetailsChangeOriginKind.System => ReservationDetailsChangeOrigin.System,
            _ => ReservationDetailsChangeOrigin.Unknown
        };
        string normalizedActorId = command.ActorId?.Trim() ?? string.Empty;
        if (origin == ReservationDetailsChangeOrigin.Unknown ||
            normalizedActorId.Length is 0 or > Reservation.ActorIdMaxLength)
        {
            return Result.Failure<ReservationMutationReceiptDto>(
                ReservationsApplicationErrors.DetailsChangeProvenanceInvalid);
        }

        ReservationManagementOperationRecord? existing = await operations
            .GetAsync(command.ReservationId, command.OperationId, cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            if (!existing.MatchesGuestDetails(command.ExpectedDetailsRevision))
            {
                return Result.Failure<ReservationMutationReceiptDto>(
                    ReservationsApplicationErrors.ManagementOperationConflict);
            }

            ReservationDetailsOperationReplay? replay = await history
                .FindOperationAsync(
                    command.PropertyId,
                    command.ReservationId,
                    command.OperationId,
                    cancellationToken)
                .ConfigureAwait(false);
            return replay is not null && Matches(replay, command, origin)
                ? Result.Success(reservation.ToMutationReceipt())
                : Result.Failure<ReservationMutationReceiptDto>(
                    ReservationsApplicationErrors.ManagementOperationConflict);
        }

        DateTimeOffset nowUtc = clock.UtcNow;
        Result<ReservationDetailsChangeOutcome> changed = reservation.UpdateGuestDetails(
            command.PrimaryGuestName,
            command.Email,
            command.Phone,
            command.GuestCount,
            command.Notes,
            command.ExpectedDetailsRevision,
            origin,
            normalizedActorId,
            adapterConnectionId: null,
            externalOperationId: null,
            command.OperationId,
            idGenerator.NewId(),
            nowUtc,
            command.ExpectedArrivalTime,
            command.ExpectedDepartureTime);
        if (changed.IsFailure)
        {
            return Result.Failure<ReservationMutationReceiptDto>(changed.Error);
        }

        if (changed.Value == ReservationDetailsChangeOutcome.Changed)
        {
            await operations.AddAsync(
                new(
                    command.OperationId,
                    reservation.ScopeId,
                    command.PropertyId,
                    command.ReservationId,
                    ReservationManagementOperationKind.GuestDetails,
                    ExpectedVersion: null,
                    command.ExpectedDetailsRevision,
                    BusinessDate: null,
                    nowUtc),
                cancellationToken).ConfigureAwait(false);
        }

        return Result.Success(reservation.ToMutationReceipt());
    }

    private static bool Matches(
        ReservationDetailsOperationReplay replay,
        UpdateReservationGuestDetailsCommand command,
        ReservationDetailsChangeOrigin origin)
    {
        ReservationDetailsSnapshotDto after = replay.After;
        return replay.ExpectedDetailsRevision == command.ExpectedDetailsRevision &&
            replay.Origin == (ReservationDetailsChangeOriginKind)(int)origin &&
            string.Equals(
                after.PrimaryGuestName,
                NormalizeRequired(command.PrimaryGuestName),
                StringComparison.Ordinal) &&
            string.Equals(after.Email, NormalizeOptional(command.Email), StringComparison.Ordinal) &&
            string.Equals(after.Phone, NormalizeOptional(command.Phone), StringComparison.Ordinal) &&
            after.GuestCount == command.GuestCount &&
            string.Equals(after.Notes, NormalizeOptional(command.Notes), StringComparison.Ordinal) &&
            after.ExpectedArrivalTime == command.ExpectedArrivalTime &&
            after.ExpectedDepartureTime == command.ExpectedDepartureTime;
    }

    private static string NormalizeRequired(string? value) => value?.Trim() ?? string.Empty;

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

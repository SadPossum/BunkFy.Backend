namespace BunkFy.Modules.Reservations.Application.Handlers;

using BunkFy.DataGovernance;
using BunkFy.Modules.Guests.Contracts;
using BunkFy.Modules.Reservations.Application.Commands;
using BunkFy.Modules.Reservations.Application.GuestRecords;
using BunkFy.Modules.Reservations.Application.Policies;
using BunkFy.Modules.Reservations.Application.Ports;
using BunkFy.Modules.Reservations.Contracts;
using BunkFy.Modules.Reservations.Domain.Aggregates;
using BunkFy.Modules.Reservations.Domain.Entities;
using BunkFy.Modules.Reservations.Domain.GuestRecords;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;
using DomainReviewReason = BunkFy.Modules.Reservations.Domain.GuestRecords.ReservationGuestRecordLinkReviewReason;

internal sealed class AdvanceReservationGuestRecordLinkCommandHandler(
    ReservationMutationCoordinator mutations,
    IReservationGuestRecordLinkProcessRepository processes,
    IReservationGuestProfileProjectionRepository guests,
    IGuestProcessingRestrictionGate restrictionGate,
    IReservationCountryPolicyAdmission countryPolicy,
    IScopeContext scopeContext,
    ISystemClock clock,
    IIdGenerator ids)
    : ICommandHandler<
        AdvanceReservationGuestRecordLinkCommand,
        ReservationGuestRecordLinkProcessDto>
{
    public async Task<Result<ReservationGuestRecordLinkProcessDto>> HandleAsync(
        AdvanceReservationGuestRecordLinkCommand command,
        CancellationToken cancellationToken)
    {
        if (!scopeContext.IsEnabled || string.IsNullOrWhiteSpace(scopeContext.ScopeId))
        {
            return Result.Failure<ReservationGuestRecordLinkProcessDto>(
                ReservationsApplicationErrors.TenantRequired);
        }

        Reservation? reservation = await mutations.AcquireRequiredContinuationAsync(
            command.PropertyId,
            command.ReservationId,
            cancellationToken).ConfigureAwait(false);
        ReservationGuestRecordLinkProcess? process = await processes
            .GetByOperationAsync(command.OperationId, cancellationToken)
            .ConfigureAwait(false);
        if (process is null)
        {
            return Result.Failure<ReservationGuestRecordLinkProcessDto>(
                ReservationsApplicationErrors.GuestRecordLinkProcessNotFound);
        }

        if (process.PropertyId != command.PropertyId ||
            process.ReservationId != command.ReservationId)
        {
            return Result.Failure<ReservationGuestRecordLinkProcessDto>(
                ReservationsApplicationErrors.GuestRecordLinkProcessConflict);
        }

        if (process.State is ReservationGuestRecordLinkProcessState.Completed or
            ReservationGuestRecordLinkProcessState.NeedsReview ||
            process.DispatchRevision != command.DispatchRevision)
        {
            return Result.Success(process.ToDto());
        }

        if (process.State != ReservationGuestRecordLinkProcessState.Ready)
        {
            return Result.Failure<ReservationGuestRecordLinkProcessDto>(
                ReservationsApplicationErrors.GuestRecordLinkTaskInvalid);
        }

        DateTimeOffset nowUtc = clock.UtcNow;
        if (reservation is null || reservation.IsAnonymised)
        {
            return Review(
                process,
                DomainReviewReason.ReservationUnavailable,
                nowUtc);
        }

        ReservationGuest? current = reservation.Guests.SingleOrDefault(guest =>
            guest.IsCurrent && guest.Role == ReservationGuestRole.Primary);
        if (current?.GuestId == process.Id)
        {
            return Complete(process, nowUtc);
        }

        if (current is not null)
        {
            return Review(
                process,
                DomainReviewReason.PrimaryGuestOccupied,
                nowUtc);
        }

        CountryPolicyDecision policy = await countryPolicy.EvaluateAsync(
            process.PropertyId,
            ReservationCountryPolicyAdmission.ReservationManagementPurpose,
            CountryPolicySurface.ApiWrite,
            ReservationCountryPolicyAdmission.AuthorizedOperatorProvenance,
            cancellationToken).ConfigureAwait(false);
        if (!policy.IsAllowed)
        {
            return Review(
                process,
                DomainReviewReason.CountryPolicyDenied,
                nowUtc);
        }

        if (!await guests.IsLinkableAsync(
            process.PropertyId,
            process.Id,
            cancellationToken).ConfigureAwait(false))
        {
            return command.IsFinalAttempt
                ? Review(
                    process,
                    DomainReviewReason.GuestUnavailable,
                    nowUtc)
                : Result.Failure<ReservationGuestRecordLinkProcessDto>(
                    ReservationsApplicationErrors.GuestRecordLinkProcessPending);
        }

        GuestProcessingRestrictionGateResult restriction =
            await restrictionGate.EvaluateAsync(
                new(
                    scopeContext.ScopeId,
                    process.PropertyId,
                    process.Id),
                cancellationToken).ConfigureAwait(false);
        if (!restriction.IsAllowed)
        {
            if (restriction.Decision is
                GuestProcessingRestrictionDecision.Restricted or
                GuestProcessingRestrictionDecision.UnsupportedContractVersion)
            {
                return Review(
                    process,
                    DomainReviewReason.GuestRestricted,
                    nowUtc);
            }

            return command.IsFinalAttempt
                ? Review(
                    process,
                    DomainReviewReason.RetryLimitReached,
                    nowUtc)
                : Result.Failure<ReservationGuestRecordLinkProcessDto>(
                    ReservationsApplicationErrors.GuestRecordLinkProcessPending);
        }

        if (string.IsNullOrWhiteSpace(process.RequestedBy))
        {
            return Result.Failure<ReservationGuestRecordLinkProcessDto>(
                ReservationsApplicationErrors.GuestRecordLinkTaskInvalid);
        }

        Result<bool> linked = reservation.LinkGuest(
            process.Id,
            ReservationGuestRole.Primary,
            replaceExistingRole: false,
            reservation.Version,
            process.RequestedBy,
            ids.NewId(),
            nowUtc);
        if (linked.IsFailure)
        {
            return linked.Error ==
                BunkFy.Modules.Reservations.Domain.Errors.ReservationsDomainErrors
                    .ReservationGuestRoleOccupied
                ? Review(
                    process,
                    DomainReviewReason.PrimaryGuestOccupied,
                    nowUtc)
                : Result.Failure<ReservationGuestRecordLinkProcessDto>(linked.Error);
        }

        return Complete(process, nowUtc);
    }

    private static Result<ReservationGuestRecordLinkProcessDto> Complete(
        ReservationGuestRecordLinkProcess process,
        DateTimeOffset nowUtc)
    {
        Result<bool> completed = process.Complete(nowUtc);
        return completed.IsSuccess
            ? Result.Success(process.ToDto())
            : Result.Failure<ReservationGuestRecordLinkProcessDto>(completed.Error);
    }

    private static Result<ReservationGuestRecordLinkProcessDto> Review(
        ReservationGuestRecordLinkProcess process,
        DomainReviewReason reason,
        DateTimeOffset nowUtc)
    {
        Result<bool> reviewed = process.RequireReview(reason, nowUtc);
        return reviewed.IsSuccess
            ? Result.Success(process.ToDto())
            : Result.Failure<ReservationGuestRecordLinkProcessDto>(reviewed.Error);
    }
}

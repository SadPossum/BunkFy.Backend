namespace BunkFy.Modules.Reservations.Application.Handlers;

using BunkFy.DataGovernance;
using BunkFy.Modules.Reservations.Application.Commands;
using BunkFy.Modules.Reservations.Application.GuestRecords;
using BunkFy.Modules.Reservations.Application.Policies;
using BunkFy.Modules.Reservations.Application.Ports;
using BunkFy.Modules.Reservations.Application.Queries;
using BunkFy.Modules.Reservations.Contracts;
using BunkFy.Modules.Reservations.Domain.Aggregates;
using BunkFy.Modules.Reservations.Domain.GuestRecords;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;

internal sealed class PrepareReservationGuestRecordLinkCommandHandler(
    ReservationMutationCoordinator mutations,
    IReservationGuestRecordLinkProcessRepository processes,
    IReservationCountryPolicyAdmission countryPolicy,
    IScopeContext scopeContext,
    ISystemClock clock,
    IIdGenerator ids)
    : ICommandHandler<
        PrepareReservationGuestRecordLinkCommand,
        ReservationGuestRecordLinkPreparationDto>
{
    public async Task<Result<ReservationGuestRecordLinkPreparationDto>> HandleAsync(
        PrepareReservationGuestRecordLinkCommand command,
        CancellationToken cancellationToken)
    {
        if (!scopeContext.IsEnabled || string.IsNullOrWhiteSpace(scopeContext.ScopeId))
        {
            return Result.Failure<ReservationGuestRecordLinkPreparationDto>(
                ReservationsApplicationErrors.TenantRequired);
        }

        CountryPolicyDecision policy = await countryPolicy.EvaluateAsync(
            command.PropertyId,
            ReservationCountryPolicyAdmission.ReservationManagementPurpose,
            CountryPolicySurface.ApiWrite,
            ReservationCountryPolicyAdmission.AuthorizedOperatorProvenance,
            cancellationToken).ConfigureAwait(false);
        if (!policy.IsAllowed)
        {
            return Result.Failure<ReservationGuestRecordLinkPreparationDto>(
                ReservationsApplicationErrors.CountryPolicyDenied(policy.Reason));
        }

        Reservation? reservation = await mutations.AcquireOperationalAsync(
            command.PropertyId,
            command.ReservationId,
            cancellationToken).ConfigureAwait(false);
        if (reservation is null)
        {
            return Result.Failure<ReservationGuestRecordLinkPreparationDto>(
                ReservationsApplicationErrors.ReservationNotFound);
        }

        ReservationGuestRecordLinkProcess? existingByOperation = await processes
            .GetByOperationAsync(command.OperationId, cancellationToken)
            .ConfigureAwait(false);
        if (existingByOperation is not null)
        {
            return existingByOperation.MatchesPreparation(
                command.PropertyId,
                command.ReservationId,
                command.ExpectedReservationVersion)
                ? Result.Success(existingByOperation.ToPreparationDto())
                : Result.Failure<ReservationGuestRecordLinkPreparationDto>(
                    ReservationsApplicationErrors.GuestRecordLinkProcessConflict);
        }

        ReservationGuestRecordLinkProcess? existingByReservation = await processes
            .GetByReservationAsync(
                command.PropertyId,
                command.ReservationId,
                cancellationToken)
            .ConfigureAwait(false);
        if (existingByReservation is not null)
        {
            return Result.Success(existingByReservation.ToPreparationDto());
        }

        if (reservation.Version != command.ExpectedReservationVersion)
        {
            return Result.Failure<ReservationGuestRecordLinkPreparationDto>(
                ReservationsApplicationErrors.VersionConflict);
        }

        if (reservation.Guests.Any(guest =>
            guest.IsCurrent && guest.Role == ReservationGuestRole.Primary))
        {
            return Result.Failure<ReservationGuestRecordLinkPreparationDto>(
                ReservationsApplicationErrors.GuestRecordLinkProcessReservationOccupied);
        }

        Result<ReservationGuestRecordLinkProcess> prepared =
            ReservationGuestRecordLinkProcess.Prepare(
                command.OperationId,
                scopeContext.ScopeId,
                command.PropertyId,
                command.ReservationId,
                ids.NewId(),
                command.ExpectedReservationVersion,
                command.ActorId,
                clock.UtcNow);
        if (prepared.IsFailure)
        {
            return Result.Failure<ReservationGuestRecordLinkPreparationDto>(
                prepared.Error);
        }

        await processes.AddAsync(prepared.Value, cancellationToken)
            .ConfigureAwait(false);
        return Result.Success(prepared.Value.ToPreparationDto());
    }
}

internal sealed class ConfirmReservationGuestRecordLinkCommandHandler(
    ReservationMutationCoordinator mutations,
    IReservationGuestRecordLinkProcessRepository processes,
    IScopeContext scopeContext,
    ISystemClock clock,
    IIdGenerator ids)
    : ICommandHandler<
        ConfirmReservationGuestRecordLinkCommand,
        ReservationGuestRecordLinkProcessDto>
{
    public async Task<Result<ReservationGuestRecordLinkProcessDto>> HandleAsync(
        ConfirmReservationGuestRecordLinkCommand command,
        CancellationToken cancellationToken)
    {
        if (!scopeContext.IsEnabled || string.IsNullOrWhiteSpace(scopeContext.ScopeId))
        {
            return Result.Failure<ReservationGuestRecordLinkProcessDto>(
                ReservationsApplicationErrors.TenantRequired);
        }

        if (!await mutations.AcquireExistingAsync(
            command.ReservationId,
            cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<ReservationGuestRecordLinkProcessDto>(
                ReservationsApplicationErrors.GuestRecordLinkProcessNotFound);
        }

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

        Result<bool> confirmed = process.ConfirmGuest(
            command.CreationConfirmationId,
            ids.NewId(),
            clock.UtcNow);
        return confirmed.IsSuccess
            ? Result.Success(process.ToDto())
            : Result.Failure<ReservationGuestRecordLinkProcessDto>(confirmed.Error);
    }
}

internal sealed class RetryReservationGuestRecordLinkCommandHandler(
    ReservationMutationCoordinator mutations,
    IReservationGuestRecordLinkProcessRepository processes,
    IReservationCountryPolicyAdmission countryPolicy,
    IScopeContext scopeContext,
    ISystemClock clock,
    IIdGenerator ids)
    : ICommandHandler<
        RetryReservationGuestRecordLinkCommand,
        ReservationGuestRecordLinkProcessDto>
{
    public async Task<Result<ReservationGuestRecordLinkProcessDto>> HandleAsync(
        RetryReservationGuestRecordLinkCommand command,
        CancellationToken cancellationToken)
    {
        if (!scopeContext.IsEnabled || string.IsNullOrWhiteSpace(scopeContext.ScopeId))
        {
            return Result.Failure<ReservationGuestRecordLinkProcessDto>(
                ReservationsApplicationErrors.TenantRequired);
        }

        CountryPolicyDecision policy = await countryPolicy.EvaluateAsync(
            command.PropertyId,
            ReservationCountryPolicyAdmission.ReservationManagementPurpose,
            CountryPolicySurface.ApiWrite,
            ReservationCountryPolicyAdmission.AuthorizedOperatorProvenance,
            cancellationToken).ConfigureAwait(false);
        if (!policy.IsAllowed)
        {
            return Result.Failure<ReservationGuestRecordLinkProcessDto>(
                ReservationsApplicationErrors.CountryPolicyDenied(policy.Reason));
        }

        if (!await mutations.AcquireExistingAsync(
            command.ReservationId,
            cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<ReservationGuestRecordLinkProcessDto>(
                ReservationsApplicationErrors.GuestRecordLinkProcessNotFound);
        }

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

        if (process.State is ReservationGuestRecordLinkProcessState.Ready or
            ReservationGuestRecordLinkProcessState.Completed)
        {
            return Result.Success(process.ToDto());
        }

        if (process.State != ReservationGuestRecordLinkProcessState.NeedsReview)
        {
            return Result.Failure<ReservationGuestRecordLinkProcessDto>(
                ReservationsApplicationErrors.GuestRecordLinkProcessRetryInvalid);
        }

        Result<bool> redispatched = process.Redispatch(ids.NewId(), clock.UtcNow);
        return redispatched.IsSuccess
            ? Result.Success(process.ToDto())
            : Result.Failure<ReservationGuestRecordLinkProcessDto>(
                redispatched.Error);
    }
}

internal sealed class GetReservationGuestRecordLinkQueryHandler(
    IReservationGuestRecordLinkProcessRepository processes,
    IScopeContext scopeContext)
    : IQueryHandler<
        GetReservationGuestRecordLinkQuery,
        ReservationGuestRecordLinkProcessDto>
{
    public async Task<Result<ReservationGuestRecordLinkProcessDto>> HandleAsync(
        GetReservationGuestRecordLinkQuery query,
        CancellationToken cancellationToken)
    {
        if (!scopeContext.IsEnabled || string.IsNullOrWhiteSpace(scopeContext.ScopeId))
        {
            return Result.Failure<ReservationGuestRecordLinkProcessDto>(
                ReservationsApplicationErrors.TenantRequired);
        }

        ReservationGuestRecordLinkProcess? process = await processes
            .GetByOperationAsync(query.OperationId, cancellationToken)
            .ConfigureAwait(false);
        if (process is null)
        {
            return Result.Failure<ReservationGuestRecordLinkProcessDto>(
                ReservationsApplicationErrors.GuestRecordLinkProcessNotFound);
        }

        return process.PropertyId == query.PropertyId &&
            process.ReservationId == query.ReservationId
            ? Result.Success(process.ToDto())
            : Result.Failure<ReservationGuestRecordLinkProcessDto>(
                ReservationsApplicationErrors.GuestRecordLinkProcessConflict);
    }
}

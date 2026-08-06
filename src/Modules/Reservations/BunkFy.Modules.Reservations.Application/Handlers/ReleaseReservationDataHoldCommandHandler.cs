namespace BunkFy.Modules.Reservations.Application.Handlers;

using BunkFy.Modules.Reservations.Application.Commands;
using BunkFy.Modules.Reservations.Application.Mapping;
using BunkFy.Modules.Reservations.Application.Ports;
using BunkFy.Modules.Reservations.Contracts;
using BunkFy.Modules.Reservations.Domain.Aggregates;
using BunkFy.Modules.Reservations.Domain.DataRights;
using BunkFy.Modules.Reservations.Domain.Models;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;
using DomainDataHoldAction =
    BunkFy.Modules.Reservations.Domain.Models.ReservationDataHoldAction;

internal sealed class ReleaseReservationDataHoldCommandHandler(
    ReservationMutationCoordinator mutations,
    IReservationDataHoldRepository holds,
    IScopeContext scopeContext,
    ISystemClock clock,
    IIdGenerator ids)
    : ICommandHandler<ReleaseReservationDataHoldCommand, ReservationDataHoldReceiptDto>
{
    public async Task<Result<ReservationDataHoldReceiptDto>> HandleAsync(
        ReleaseReservationDataHoldCommand command,
        CancellationToken cancellationToken)
    {
        string? tenantId = scopeContext.IsEnabled ? scopeContext.ScopeId : null;
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Result.Failure<ReservationDataHoldReceiptDto>(
                ReservationsApplicationErrors.TenantRequired);
        }

        string? actorId = NormalizeActor(command.ActorId);
        if (command.IdempotencyKey == Guid.Empty ||
            command.PropertyId == Guid.Empty ||
            command.ReservationId == Guid.Empty ||
            command.HoldId == Guid.Empty ||
            command.ExpectedReservationVersion < 1 ||
            command.ExpectedDetailsRevision < 1 ||
            command.ExpectedHoldVersion < 1 ||
            actorId is null)
        {
            return Result.Failure<ReservationDataHoldReceiptDto>(
                ReservationsApplicationErrors.DataHoldRequestInvalid);
        }

        ReservationDataHoldReceipt? existing =
            await holds.FindReceiptByIdempotencyKeyAsync(
                command.IdempotencyKey,
                cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return await ReplayAsync(
                existing,
                command,
                actorId,
                holds,
                cancellationToken).ConfigureAwait(false);
        }

        Reservation? reservation = await mutations.AcquireDataRightsAsync(
            command.PropertyId,
            command.ReservationId,
            cancellationToken).ConfigureAwait(false);
        if (reservation is null)
        {
            return Result.Failure<ReservationDataHoldReceiptDto>(
                ReservationsApplicationErrors.ReservationNotFound);
        }

        if (reservation.Version != command.ExpectedReservationVersion)
        {
            return Result.Failure<ReservationDataHoldReceiptDto>(
                ReservationsApplicationErrors.DataHoldReservationVersionConflict);
        }

        if (reservation.DetailsRevision != command.ExpectedDetailsRevision)
        {
            return Result.Failure<ReservationDataHoldReceiptDto>(
                ReservationsApplicationErrors.DataHoldDetailsRevisionConflict);
        }

        ReservationDataHold? hold = await holds.GetAsync(
            command.PropertyId,
            command.ReservationId,
            command.HoldId,
            cancellationToken).ConfigureAwait(false);
        if (hold is null)
        {
            return Result.Failure<ReservationDataHoldReceiptDto>(
                ReservationsApplicationErrors.DataHoldNotFound);
        }

        DateTimeOffset nowUtc = ToPersistencePrecision(clock.UtcNow);
        Result released = hold.Release(
            command.ExpectedHoldVersion,
            actorId,
            nowUtc);
        if (released.IsFailure)
        {
            return Result.Failure<ReservationDataHoldReceiptDto>(released.Error);
        }

        Result<ReservationDataHoldReceipt> receipt =
            ReservationDataHoldReceipt.Create(
                ids.NewId(),
                tenantId,
                command.IdempotencyKey,
                hold,
                DomainDataHoldAction.Release,
                command.ExpectedReservationVersion,
                command.ExpectedDetailsRevision,
                nowUtc);
        if (receipt.IsFailure)
        {
            return Result.Failure<ReservationDataHoldReceiptDto>(receipt.Error);
        }

        await holds.AddReceiptAsync(receipt.Value, cancellationToken)
            .ConfigureAwait(false);
        return Result.Success(receipt.Value.ToDto());
    }

    private static async Task<Result<ReservationDataHoldReceiptDto>> ReplayAsync(
        ReservationDataHoldReceipt receipt,
        ReleaseReservationDataHoldCommand command,
        string actorId,
        IReservationDataHoldRepository holds,
        CancellationToken cancellationToken)
    {
        if (receipt.Action != DomainDataHoldAction.Release ||
            receipt.HoldId != command.HoldId ||
            receipt.PropertyId != command.PropertyId ||
            receipt.ReservationId != command.ReservationId ||
            receipt.SelectedReservationVersion !=
                command.ExpectedReservationVersion ||
            receipt.SelectedDetailsRevision != command.ExpectedDetailsRevision ||
            receipt.ResultingHoldVersion - 1 != command.ExpectedHoldVersion)
        {
            return Result.Failure<ReservationDataHoldReceiptDto>(
                ReservationsApplicationErrors.DataHoldIdempotencyConflict);
        }

        ReservationDataHold? hold = await holds.GetAsync(
            command.PropertyId,
            command.ReservationId,
            command.HoldId,
            cancellationToken).ConfigureAwait(false);
        if (hold is null ||
            !string.Equals(hold.ReleasedBy, actorId, StringComparison.Ordinal) ||
            hold.ReleasedAtUtc != receipt.CompletedAtUtc)
        {
            return Result.Failure<ReservationDataHoldReceiptDto>(
                ReservationsApplicationErrors.DataHoldProofUnavailable);
        }

        return Result.Success(receipt.ToDto());
    }

    private static string? NormalizeActor(string? actorId)
    {
        string? normalized = actorId?.Trim();
        return normalized is { Length: > 0 } &&
            normalized.Length <= Reservation.ActorIdMaxLength
            ? normalized
            : null;
    }

    private static DateTimeOffset ToPersistencePrecision(DateTimeOffset value)
    {
        const long ticksPerMicrosecond = TimeSpan.TicksPerMillisecond / 1000;
        return new(value.Ticks - (value.Ticks % ticksPerMicrosecond), value.Offset);
    }
}

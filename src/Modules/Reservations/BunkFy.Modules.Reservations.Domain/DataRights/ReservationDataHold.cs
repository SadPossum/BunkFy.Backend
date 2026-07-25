namespace BunkFy.Modules.Reservations.Domain.DataRights;

using BunkFy.Modules.Reservations.Domain.Aggregates;
using BunkFy.Modules.Reservations.Domain.Errors;
using BunkFy.Modules.Reservations.Domain.Models;
using Gma.Framework.Domain.Models;
using Gma.Framework.Naming;
using Gma.Framework.Results;

public sealed class ReservationDataHold : ScopedAggregateRoot<Guid>
{
    public const int ReasonCodeMaxLength = 128;

    private ReservationDataHold() { }

    private ReservationDataHold(Guid id, string scopeId)
        : base(id, scopeId)
    {
    }

    public Guid PropertyId { get; private set; }
    public Guid ReservationId { get; private set; }
    public string ReasonCode { get; private set; } = string.Empty;
    public ReservationDataHoldState State { get; private set; } =
        ReservationDataHoldState.Active;
    public string PlacedBy { get; private set; } = string.Empty;
    public DateTimeOffset PlacedAtUtc { get; private set; }
    public string? ReleasedBy { get; private set; }
    public DateTimeOffset? ReleasedAtUtc { get; private set; }
    public long Version { get; private set; } = 1;

    public static Result<ReservationDataHold> Place(
        Guid id,
        string tenantId,
        Guid propertyId,
        Guid reservationId,
        string reasonCode,
        string actorId,
        DateTimeOffset nowUtc)
    {
        if (id == Guid.Empty ||
            propertyId == Guid.Empty ||
            reservationId == Guid.Empty ||
            !TenantIds.TryNormalize(tenantId, out string? scopeId))
        {
            return Result.Failure<ReservationDataHold>(
                ReservationsDomainErrors.DataHoldIdentityInvalid);
        }

        string normalizedReason = NormalizeReasonCode(reasonCode);
        string normalizedActor = actorId?.Trim() ?? string.Empty;
        if (normalizedReason.Length is 0 or > ReasonCodeMaxLength)
        {
            return Result.Failure<ReservationDataHold>(
                ReservationsDomainErrors.DataHoldReasonCodeInvalid);
        }

        if (normalizedActor.Length is 0 or > Reservation.ActorIdMaxLength)
        {
            return Result.Failure<ReservationDataHold>(
                ReservationsDomainErrors.DataHoldActorInvalid);
        }

        if (nowUtc == default)
        {
            return Result.Failure<ReservationDataHold>(
                ReservationsDomainErrors.DataHoldLifecycleInvalid);
        }

        return Result.Success(new ReservationDataHold(id, scopeId)
        {
            PropertyId = propertyId,
            ReservationId = reservationId,
            ReasonCode = normalizedReason,
            PlacedBy = normalizedActor,
            PlacedAtUtc = nowUtc
        });
    }

    public Result Release(
        long expectedVersion,
        string actorId,
        DateTimeOffset nowUtc)
    {
        if (expectedVersion != this.Version)
        {
            return Result.Failure(
                ReservationsDomainErrors.DataHoldVersionConflict);
        }

        if (this.State != ReservationDataHoldState.Active)
        {
            return Result.Failure(
                ReservationsDomainErrors.DataHoldAlreadyReleased);
        }

        string normalizedActor = actorId?.Trim() ?? string.Empty;
        if (normalizedActor.Length is 0 or > Reservation.ActorIdMaxLength)
        {
            return Result.Failure(
                ReservationsDomainErrors.DataHoldActorInvalid);
        }

        if (nowUtc == default || nowUtc < this.PlacedAtUtc)
        {
            return Result.Failure(
                ReservationsDomainErrors.DataHoldLifecycleInvalid);
        }

        this.State = ReservationDataHoldState.Released;
        this.ReleasedBy = normalizedActor;
        this.ReleasedAtUtc = nowUtc;
        this.Version++;
        return Result.Success();
    }

    private static string NormalizeReasonCode(string? reasonCode) =>
        reasonCode?.Trim().ToLowerInvariant() ?? string.Empty;
}

namespace BunkFy.Modules.Guests.Domain.DataRights;

using BunkFy.Modules.Guests.Domain.Aggregates;
using BunkFy.Modules.Guests.Domain.Errors;
using BunkFy.Modules.Guests.Domain.Models;
using Gma.Framework.Domain.Models;
using Gma.Framework.Naming;
using Gma.Framework.Results;

public sealed class GuestDataHold : ScopedAggregateRoot<Guid>
{
    public const int ReasonCodeMaxLength = 128;

    private GuestDataHold() { }

    private GuestDataHold(Guid id, string scopeId)
        : base(id, scopeId)
    {
    }

    public Guid PropertyId { get; private set; }
    public Guid GuestId { get; private set; }
    public string ReasonCode { get; private set; } = string.Empty;
    public GuestDataHoldState State { get; private set; } = GuestDataHoldState.Active;
    public string PlacedBy { get; private set; } = string.Empty;
    public DateTimeOffset PlacedAtUtc { get; private set; }
    public string? ReleasedBy { get; private set; }
    public DateTimeOffset? ReleasedAtUtc { get; private set; }
    public long Version { get; private set; } = 1;

    public static Result<GuestDataHold> Place(
        Guid id,
        string tenantId,
        Guid propertyId,
        Guid guestId,
        string reasonCode,
        string actorId,
        DateTimeOffset nowUtc)
    {
        if (id == Guid.Empty ||
            propertyId == Guid.Empty ||
            guestId == Guid.Empty ||
            !TenantIds.TryNormalize(tenantId, out string? scopeId))
        {
            return Result.Failure<GuestDataHold>(GuestsDomainErrors.DataHoldIdentityInvalid);
        }

        string normalizedReason = NormalizeReasonCode(reasonCode);
        string normalizedActor = actorId?.Trim() ?? string.Empty;
        if (normalizedReason.Length is 0 or > ReasonCodeMaxLength)
        {
            return Result.Failure<GuestDataHold>(GuestsDomainErrors.DataHoldReasonCodeInvalid);
        }

        if (normalizedActor.Length is 0 or > GuestProfile.ActorIdMaxLength)
        {
            return Result.Failure<GuestDataHold>(GuestsDomainErrors.ActorInvalid);
        }

        if (nowUtc == default)
        {
            return Result.Failure<GuestDataHold>(GuestsDomainErrors.DataHoldLifecycleInvalid);
        }

        return Result.Success(new GuestDataHold(id, scopeId)
        {
            PropertyId = propertyId,
            GuestId = guestId,
            ReasonCode = normalizedReason,
            PlacedBy = normalizedActor,
            PlacedAtUtc = nowUtc
        });
    }

    public Result Release(long expectedVersion, string actorId, DateTimeOffset nowUtc)
    {
        if (expectedVersion != this.Version)
        {
            return Result.Failure(GuestsDomainErrors.DataHoldVersionConflict);
        }

        if (this.State != GuestDataHoldState.Active)
        {
            return Result.Failure(GuestsDomainErrors.DataHoldAlreadyReleased);
        }

        string normalizedActor = actorId?.Trim() ?? string.Empty;
        if (normalizedActor.Length is 0 or > GuestProfile.ActorIdMaxLength)
        {
            return Result.Failure(GuestsDomainErrors.ActorInvalid);
        }

        if (nowUtc == default || nowUtc < this.PlacedAtUtc)
        {
            return Result.Failure(GuestsDomainErrors.DataHoldLifecycleInvalid);
        }

        this.State = GuestDataHoldState.Released;
        this.ReleasedBy = normalizedActor;
        this.ReleasedAtUtc = nowUtc;
        this.Version++;
        return Result.Success();
    }

    private static string NormalizeReasonCode(string? reasonCode) =>
        reasonCode?.Trim().ToLowerInvariant() ?? string.Empty;
}

namespace BunkFy.Modules.Ingestion.Domain.LegalHolds;

using Gma.Framework.Domain.Models;
using Gma.Framework.Results;
using BunkFy.Modules.Ingestion.Domain.Errors;

public sealed class LegalHold : ScopedAggregateRoot<Guid>
{
    public const int ReasonMaxLength = 500;
    public const int ActorMaxLength = 200;

    private LegalHold() { }

    private LegalHold(Guid id, string scopeId)
        : base(id, scopeId)
    {
    }

    public Guid PropertyId { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public LegalHoldState State { get; private set; } = LegalHoldState.Active;
    public string PlacedBy { get; private set; } = string.Empty;
    public DateTimeOffset PlacedAtUtc { get; private set; }
    public string? ReleasedBy { get; private set; }
    public string? ReleaseReason { get; private set; }
    public DateTimeOffset? ReleasedAtUtc { get; private set; }
    public long Version { get; private set; } = 1;

    public static Result<LegalHold> Place(
        Guid id,
        string scopeId,
        Guid propertyId,
        string reason,
        string placedBy,
        DateTimeOffset nowUtc)
    {
        string normalizedScope = scopeId?.Trim() ?? string.Empty;
        string normalizedReason = reason?.Trim() ?? string.Empty;
        string normalizedActor = placedBy?.Trim() ?? string.Empty;
        if (id == Guid.Empty || propertyId == Guid.Empty || normalizedScope.Length == 0 || nowUtc == default)
        {
            return Result.Failure<LegalHold>(IngestionDomainErrors.LegalHoldIdentityInvalid);
        }

        if (normalizedReason.Length is 0 or > ReasonMaxLength)
        {
            return Result.Failure<LegalHold>(IngestionDomainErrors.LegalHoldReasonInvalid);
        }

        if (normalizedActor.Length is 0 or > ActorMaxLength)
        {
            return Result.Failure<LegalHold>(IngestionDomainErrors.LegalHoldActorInvalid);
        }

        return Result.Success(new LegalHold(id, normalizedScope)
        {
            PropertyId = propertyId,
            Reason = normalizedReason,
            PlacedBy = normalizedActor,
            PlacedAtUtc = nowUtc
        });
    }

    public Result Release(
        long expectedVersion,
        string releasedBy,
        string releaseReason,
        DateTimeOffset nowUtc)
    {
        if (expectedVersion != this.Version)
        {
            return Result.Failure(IngestionDomainErrors.VersionConflict);
        }

        if (this.State != LegalHoldState.Active)
        {
            return Result.Failure(IngestionDomainErrors.LegalHoldAlreadyReleased);
        }

        string normalizedActor = releasedBy?.Trim() ?? string.Empty;
        string normalizedReason = releaseReason?.Trim() ?? string.Empty;
        if (normalizedActor.Length is 0 or > ActorMaxLength)
        {
            return Result.Failure(IngestionDomainErrors.LegalHoldActorInvalid);
        }

        if (normalizedReason.Length is 0 or > ReasonMaxLength)
        {
            return Result.Failure(IngestionDomainErrors.LegalHoldReleaseReasonInvalid);
        }

        if (nowUtc == default || nowUtc < this.PlacedAtUtc)
        {
            return Result.Failure(IngestionDomainErrors.LegalHoldLifecycleInvalid);
        }

        this.State = LegalHoldState.Released;
        this.ReleasedBy = normalizedActor;
        this.ReleaseReason = normalizedReason;
        this.ReleasedAtUtc = nowUtc;
        this.Version++;
        return Result.Success();
    }
}

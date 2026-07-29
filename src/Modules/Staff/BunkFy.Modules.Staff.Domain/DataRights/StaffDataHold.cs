namespace BunkFy.Modules.Staff.Domain.DataRights;

using BunkFy.Modules.Staff.Domain.Aggregates;
using BunkFy.Modules.Staff.Domain.Errors;
using BunkFy.Modules.Staff.Domain.Models;
using Gma.Framework.Domain.Models;
using Gma.Framework.Naming;
using Gma.Framework.Results;

public sealed class StaffDataHold : ScopedAggregateRoot<Guid>
{
    public const int ReasonCodeMaxLength = 128;
    public const int MaximumRecordsPerStaffMember = 1_000;

    private StaffDataHold() { }

    private StaffDataHold(Guid id, string scopeId)
        : base(id, scopeId)
    {
    }

    public Guid StaffMemberId { get; private set; }
    public string ReasonCode { get; private set; } = string.Empty;
    public StaffDataHoldState State { get; private set; } =
        StaffDataHoldState.Active;
    public string PlacedBy { get; private set; } = string.Empty;
    public DateTimeOffset PlacedAtUtc { get; private set; }
    public string? ReleasedBy { get; private set; }
    public DateTimeOffset? ReleasedAtUtc { get; private set; }
    public long Version { get; private set; } = 1;

    public static Result<StaffDataHold> Place(
        Guid id,
        string tenantId,
        Guid staffMemberId,
        string reasonCode,
        string actorId,
        DateTimeOffset nowUtc)
    {
        if (id == Guid.Empty ||
            staffMemberId == Guid.Empty ||
            !TenantIds.TryNormalize(tenantId, out string? scopeId))
        {
            return Result.Failure<StaffDataHold>(
                StaffDomainErrors.DataHoldIdentityInvalid);
        }

        string normalizedReason = NormalizeReasonCode(reasonCode);
        string normalizedActor = actorId?.Trim() ?? string.Empty;
        if (normalizedReason.Length is 0 or > ReasonCodeMaxLength)
        {
            return Result.Failure<StaffDataHold>(
                StaffDomainErrors.DataHoldReasonCodeInvalid);
        }

        if (normalizedActor.Length is 0 or > StaffMember.ActorIdMaxLength ||
            normalizedActor.Any(char.IsControl))
        {
            return Result.Failure<StaffDataHold>(
                StaffDomainErrors.ActorInvalid);
        }

        if (nowUtc == default)
        {
            return Result.Failure<StaffDataHold>(
                StaffDomainErrors.DataHoldLifecycleInvalid);
        }

        return Result.Success(new StaffDataHold(id, scopeId)
        {
            StaffMemberId = staffMemberId,
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
                StaffDomainErrors.DataHoldVersionConflict);
        }

        if (this.State != StaffDataHoldState.Active)
        {
            return Result.Failure(
                StaffDomainErrors.DataHoldAlreadyReleased);
        }

        string normalizedActor = actorId?.Trim() ?? string.Empty;
        if (normalizedActor.Length is 0 or > StaffMember.ActorIdMaxLength ||
            normalizedActor.Any(char.IsControl))
        {
            return Result.Failure(StaffDomainErrors.ActorInvalid);
        }

        if (nowUtc == default || nowUtc < this.PlacedAtUtc)
        {
            return Result.Failure(
                StaffDomainErrors.DataHoldLifecycleInvalid);
        }

        this.State = StaffDataHoldState.Released;
        this.ReleasedBy = normalizedActor;
        this.ReleasedAtUtc = nowUtc;
        this.Version++;
        return Result.Success();
    }

    private static string NormalizeReasonCode(string? reasonCode) =>
        reasonCode?.Trim().ToLowerInvariant() ?? string.Empty;
}

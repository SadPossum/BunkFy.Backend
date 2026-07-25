namespace BunkFy.Modules.Reservations.Domain.DataRights;

using BunkFy.Modules.Reservations.Domain.Errors;
using Gma.Framework.Domain;
using Gma.Framework.Naming;
using Gma.Framework.Results;

public sealed class ReservationProcessingRestrictionProjection : IScopedEntity
{
    private ReservationProcessingRestrictionProjection() { }

    private ReservationProcessingRestrictionProjection(
        string scopeId,
        Guid propertyId,
        Guid reservationId,
        int contractVersion,
        DateTimeOffset initializedAtUtc)
    {
        this.ScopeId = scopeId;
        this.PropertyId = propertyId;
        this.ReservationId = reservationId;
        this.ContractVersion = contractVersion;
        this.LastTransitionAtUtc = initializedAtUtc;
    }

    public string ScopeId { get; private set; } = string.Empty;
    public Guid PropertyId { get; private set; }
    public Guid ReservationId { get; private set; }
    public long ProjectionOrdinal { get; private set; }
    public int ContractVersion { get; private set; }
    public long Revision { get; private set; }
    public int ActiveRestrictionCount { get; private set; }
    public bool IsRestricted { get; private set; }
    public DateTimeOffset LastTransitionAtUtc { get; private set; }

    public static Result<ReservationProcessingRestrictionProjection> Create(
        string tenantId,
        Guid propertyId,
        Guid reservationId,
        int contractVersion,
        DateTimeOffset initializedAtUtc)
    {
        if (!TenantIds.TryNormalize(tenantId, out string? scopeId) ||
            propertyId == Guid.Empty ||
            reservationId == Guid.Empty ||
            contractVersion < 1 ||
            initializedAtUtc == default)
        {
            return Result.Failure<ReservationProcessingRestrictionProjection>(
                ReservationsDomainErrors.ProcessingRestrictionProjectionIdentityInvalid);
        }

        return Result.Success(new ReservationProcessingRestrictionProjection(
            scopeId,
            propertyId,
            reservationId,
            contractVersion,
            initializedAtUtc));
    }

    public Result Apply(
        long expectedRevision,
        int supportedContractVersion,
        DateTimeOffset occurredAtUtc)
    {
        Result validation = this.ValidateTransition(
            expectedRevision,
            supportedContractVersion,
            occurredAtUtc);
        if (validation.IsFailure)
        {
            return validation;
        }

        if (this.ActiveRestrictionCount == int.MaxValue)
        {
            return Result.Failure(
                ReservationsDomainErrors.ProcessingRestrictionProjectionStateInvalid);
        }

        this.ActiveRestrictionCount++;
        this.IsRestricted = true;
        this.Revision++;
        this.LastTransitionAtUtc = occurredAtUtc;
        return Result.Success();
    }

    public Result Release(
        long expectedRevision,
        int supportedContractVersion,
        DateTimeOffset occurredAtUtc)
    {
        Result validation = this.ValidateRelease(
            expectedRevision,
            supportedContractVersion,
            occurredAtUtc);
        if (validation.IsFailure)
        {
            return validation;
        }

        this.ActiveRestrictionCount--;
        this.IsRestricted = this.ActiveRestrictionCount > 0;
        this.Revision++;
        this.LastTransitionAtUtc = occurredAtUtc;
        return Result.Success();
    }

    public Result ValidateRelease(
        long expectedRevision,
        int supportedContractVersion,
        DateTimeOffset occurredAtUtc)
    {
        Result validation = this.ValidateTransition(
            expectedRevision,
            supportedContractVersion,
            occurredAtUtc);
        if (validation.IsFailure)
        {
            return validation;
        }

        return this.ActiveRestrictionCount > 0
            ? Result.Success()
            : Result.Failure(
                ReservationsDomainErrors.ProcessingRestrictionProjectionStateInvalid);
    }

    public Result ReplaceForRebuild(
        int contractVersion,
        long revision,
        int activeRestrictionCount,
        DateTimeOffset lastTransitionAtUtc)
    {
        if (contractVersion < 1 ||
            revision < 0 ||
            activeRestrictionCount < 0 ||
            lastTransitionAtUtc == default ||
            (revision == 0 && activeRestrictionCount != 0) ||
            activeRestrictionCount > revision)
        {
            return Result.Failure(
                ReservationsDomainErrors.ProcessingRestrictionProjectionStateInvalid);
        }

        this.ContractVersion = contractVersion;
        this.Revision = revision;
        this.ActiveRestrictionCount = activeRestrictionCount;
        this.IsRestricted = activeRestrictionCount > 0;
        this.LastTransitionAtUtc = lastTransitionAtUtc;
        return Result.Success();
    }

    private Result ValidateTransition(
        long expectedRevision,
        int supportedContractVersion,
        DateTimeOffset occurredAtUtc)
    {
        if (this.ContractVersion != supportedContractVersion ||
            supportedContractVersion < 1)
        {
            return Result.Failure(
                ReservationsDomainErrors.ProcessingRestrictionProjectionContractUnsupported);
        }

        if (expectedRevision != this.Revision)
        {
            return Result.Failure(
                ReservationsDomainErrors.ProcessingRestrictionProjectionVersionConflict);
        }

        if (occurredAtUtc == default || occurredAtUtc < this.LastTransitionAtUtc)
        {
            return Result.Failure(
                ReservationsDomainErrors.ProcessingRestrictionProjectionTransitionInvalid);
        }

        return Result.Success();
    }
}

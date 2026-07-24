namespace BunkFy.Modules.Guests.Domain.DataRights;

using BunkFy.Modules.Guests.Domain.Errors;
using Gma.Framework.Domain;
using Gma.Framework.Naming;
using Gma.Framework.Results;

public sealed class GuestProcessingRestrictionProjection : IScopedEntity
{
    private GuestProcessingRestrictionProjection() { }

    private GuestProcessingRestrictionProjection(
        string scopeId,
        Guid propertyId,
        Guid guestId,
        int contractVersion,
        DateTimeOffset initializedAtUtc)
    {
        this.ScopeId = scopeId;
        this.PropertyId = propertyId;
        this.GuestId = guestId;
        this.ContractVersion = contractVersion;
        this.LastTransitionAtUtc = initializedAtUtc;
    }

    public string ScopeId { get; private set; } = string.Empty;
    public Guid PropertyId { get; private set; }
    public Guid GuestId { get; private set; }
    public long ProjectionOrdinal { get; private set; }
    public int ContractVersion { get; private set; }
    public long Revision { get; private set; }
    public int ActiveRestrictionCount { get; private set; }
    public bool IsRestricted { get; private set; }
    public DateTimeOffset LastTransitionAtUtc { get; private set; }

    public static Result<GuestProcessingRestrictionProjection> Create(
        string tenantId,
        Guid propertyId,
        Guid guestId,
        int contractVersion,
        DateTimeOffset initializedAtUtc)
    {
        if (!TenantIds.TryNormalize(tenantId, out string? scopeId) ||
            propertyId == Guid.Empty ||
            guestId == Guid.Empty ||
            contractVersion < 1 ||
            initializedAtUtc == default)
        {
            return Result.Failure<GuestProcessingRestrictionProjection>(
                GuestsDomainErrors.RestrictionProjectionIdentityInvalid);
        }

        return Result.Success(new GuestProcessingRestrictionProjection(
            scopeId,
            propertyId,
            guestId,
            contractVersion,
            initializedAtUtc));
    }

    public Result Apply(
        long expectedRevision,
        int supportedContractVersion,
        DateTimeOffset occurredAtUtc)
    {
        Result validation = this.ValidateApply(
            expectedRevision,
            supportedContractVersion,
            occurredAtUtc);
        if (validation.IsFailure)
        {
            return validation;
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

    public Result ValidateApply(
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

        return this.ActiveRestrictionCount < int.MaxValue
            ? Result.Success()
            : Result.Failure(GuestsDomainErrors.RestrictionProjectionStateInvalid);
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
            : Result.Failure(GuestsDomainErrors.RestrictionProjectionStateInvalid);
    }

    private Result ValidateTransition(
        long expectedRevision,
        int supportedContractVersion,
        DateTimeOffset occurredAtUtc)
    {
        if (this.ContractVersion != supportedContractVersion ||
            supportedContractVersion < 1)
        {
            return Result.Failure(GuestsDomainErrors.RestrictionProjectionContractUnsupported);
        }

        if (expectedRevision != this.Revision)
        {
            return Result.Failure(GuestsDomainErrors.RestrictionProjectionVersionConflict);
        }

        if (occurredAtUtc == default || occurredAtUtc < this.LastTransitionAtUtc)
        {
            return Result.Failure(GuestsDomainErrors.RestrictionProjectionTransitionInvalid);
        }

        return Result.Success();
    }
}

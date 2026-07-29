namespace BunkFy.Modules.Staff.Domain.DataRights;

using BunkFy.Modules.Staff.Domain.Errors;
using Gma.Framework.Domain;
using Gma.Framework.Naming;
using Gma.Framework.Results;

public sealed class StaffProcessingRestrictionProjection : IScopedEntity
{
    private StaffProcessingRestrictionProjection() { }

    private StaffProcessingRestrictionProjection(
        string scopeId,
        Guid staffMemberId,
        int contractVersion,
        DateTimeOffset initializedAtUtc)
    {
        this.ScopeId = scopeId;
        this.StaffMemberId = staffMemberId;
        this.ContractVersion = contractVersion;
        this.LastTransitionAtUtc = initializedAtUtc;
    }

    public string ScopeId { get; private set; } = string.Empty;
    public Guid StaffMemberId { get; private set; }
    public long ProjectionOrdinal { get; private set; }
    public int ContractVersion { get; private set; }
    public long Revision { get; private set; }
    public int ActiveRestrictionCount { get; private set; }
    public bool IsRestricted { get; private set; }
    public DateTimeOffset LastTransitionAtUtc { get; private set; }

    public static Result<StaffProcessingRestrictionProjection> Create(
        string tenantId,
        Guid staffMemberId,
        int contractVersion,
        DateTimeOffset initializedAtUtc)
    {
        if (!TenantIds.TryNormalize(tenantId, out string? scopeId) ||
            staffMemberId == Guid.Empty ||
            contractVersion < 1 ||
            initializedAtUtc == default)
        {
            return Result.Failure<StaffProcessingRestrictionProjection>(
                StaffDomainErrors.RestrictionProjectionIdentityInvalid);
        }

        return Result.Success(new StaffProcessingRestrictionProjection(
            scopeId,
            staffMemberId,
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
            : Result.Failure(StaffDomainErrors.RestrictionProjectionStateInvalid);
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
            : Result.Failure(StaffDomainErrors.RestrictionProjectionStateInvalid);
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
                StaffDomainErrors.RestrictionProjectionContractUnsupported);
        }

        if (expectedRevision != this.Revision)
        {
            return Result.Failure(
                StaffDomainErrors.RestrictionProjectionVersionConflict);
        }

        if (occurredAtUtc == default || occurredAtUtc < this.LastTransitionAtUtc)
        {
            return Result.Failure(
                StaffDomainErrors.RestrictionProjectionTransitionInvalid);
        }

        return Result.Success();
    }
}

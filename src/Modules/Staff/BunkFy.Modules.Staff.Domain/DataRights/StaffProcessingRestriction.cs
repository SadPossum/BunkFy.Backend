namespace BunkFy.Modules.Staff.Domain.DataRights;

using BunkFy.Modules.Staff.Domain.Aggregates;
using BunkFy.Modules.Staff.Domain.Errors;
using BunkFy.Modules.Staff.Domain.Models;
using Gma.Framework.Domain.Models;
using Gma.Framework.Naming;
using Gma.Framework.Results;

public sealed class StaffProcessingRestriction : ScopedAggregateRoot<Guid>
{
    private StaffProcessingRestriction() { }

    private StaffProcessingRestriction(Guid id, string scopeId)
        : base(id, scopeId)
    {
    }

    public Guid StaffMemberId { get; private set; }
    public Guid ApplyCaseId { get; private set; }
    public long ApplyApprovalRevision { get; private set; }
    public long ApplySelectedStaffVersion { get; private set; }
    public StaffProcessingRestrictionState Status { get; private set; }
    public long Version { get; private set; } = 1;
    public string AppliedBy { get; private set; } = string.Empty;
    public DateTimeOffset AppliedAtUtc { get; private set; }
    public Guid? ReleaseCaseId { get; private set; }
    public long? ReleaseApprovalRevision { get; private set; }
    public long? ReleaseSelectedStaffVersion { get; private set; }
    public string? ReleasedBy { get; private set; }
    public DateTimeOffset? ReleasedAtUtc { get; private set; }

    public static Result<StaffProcessingRestriction> Create(
        Guid restrictionId,
        string tenantId,
        Guid staffMemberId,
        Guid applyCaseId,
        long applyApprovalRevision,
        long applySelectedStaffVersion,
        string actorId,
        DateTimeOffset appliedAtUtc)
    {
        if (restrictionId == Guid.Empty ||
            staffMemberId == Guid.Empty ||
            applyCaseId == Guid.Empty ||
            !TenantIds.TryNormalize(tenantId, out string? scopeId))
        {
            return Result.Failure<StaffProcessingRestriction>(
                StaffDomainErrors.RestrictionIdentityInvalid);
        }

        if (applyApprovalRevision < 1 || applySelectedStaffVersion < 1)
        {
            return Result.Failure<StaffProcessingRestriction>(
                StaffDomainErrors.RestrictionApprovalInvalid);
        }

        string? normalizedActorId = NormalizeActor(actorId);
        if (normalizedActorId is null || appliedAtUtc == default)
        {
            return Result.Failure<StaffProcessingRestriction>(
                StaffDomainErrors.RestrictionTransitionInvalid);
        }

        return Result.Success(new StaffProcessingRestriction(restrictionId, scopeId)
        {
            StaffMemberId = staffMemberId,
            ApplyCaseId = applyCaseId,
            ApplyApprovalRevision = applyApprovalRevision,
            ApplySelectedStaffVersion = applySelectedStaffVersion,
            Status = StaffProcessingRestrictionState.Active,
            AppliedBy = normalizedActorId,
            AppliedAtUtc = appliedAtUtc
        });
    }

    public Result Release(
        Guid releaseCaseId,
        long releaseApprovalRevision,
        long releaseSelectedStaffVersion,
        long expectedVersion,
        string actorId,
        DateTimeOffset releasedAtUtc)
    {
        Result validation = this.ValidateRelease(
            releaseCaseId,
            releaseApprovalRevision,
            releaseSelectedStaffVersion,
            expectedVersion,
            actorId,
            releasedAtUtc);
        if (validation.IsFailure)
        {
            return validation;
        }

        this.ReleaseCaseId = releaseCaseId;
        this.ReleaseApprovalRevision = releaseApprovalRevision;
        this.ReleaseSelectedStaffVersion = releaseSelectedStaffVersion;
        this.ReleasedBy = NormalizeActor(actorId)!;
        this.ReleasedAtUtc = releasedAtUtc;
        this.Status = StaffProcessingRestrictionState.Released;
        this.Version++;
        return Result.Success();
    }

    public Result ValidateRelease(
        Guid releaseCaseId,
        long releaseApprovalRevision,
        long releaseSelectedStaffVersion,
        long expectedVersion,
        string actorId,
        DateTimeOffset releasedAtUtc)
    {
        if (expectedVersion != this.Version)
        {
            return Result.Failure(StaffDomainErrors.RestrictionVersionConflict);
        }

        if (this.Status != StaffProcessingRestrictionState.Active)
        {
            return Result.Failure(StaffDomainErrors.RestrictionAlreadyReleased);
        }

        if (releaseCaseId == Guid.Empty ||
            releaseApprovalRevision < 1 ||
            releaseSelectedStaffVersion < 1)
        {
            return Result.Failure(StaffDomainErrors.RestrictionApprovalInvalid);
        }

        string? normalizedActorId = NormalizeActor(actorId);
        if (normalizedActorId is null ||
            releasedAtUtc == default ||
            releasedAtUtc < this.AppliedAtUtc)
        {
            return Result.Failure(StaffDomainErrors.RestrictionTransitionInvalid);
        }

        return Result.Success();
    }

    private static string? NormalizeActor(string? actorId)
    {
        string? normalized = actorId?.Trim();
        return normalized is { Length: > 0 } &&
            normalized.Length <= StaffMember.ActorIdMaxLength
            ? normalized
            : null;
    }
}

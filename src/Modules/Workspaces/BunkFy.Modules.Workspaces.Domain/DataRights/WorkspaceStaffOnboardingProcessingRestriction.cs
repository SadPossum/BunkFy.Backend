namespace BunkFy.Modules.Workspaces.Domain.DataRights;

using Gma.Framework.Domain.Models;
using Gma.Framework.Naming;
using Gma.Framework.Results;

public sealed class WorkspaceStaffOnboardingProcessingRestriction
    : ScopedAggregateRoot<Guid>
{
    private WorkspaceStaffOnboardingProcessingRestriction() { }

    private WorkspaceStaffOnboardingProcessingRestriction(
        Guid id,
        string scopeId)
        : base(id, scopeId)
    {
    }

    public Guid ApplicationId { get; private set; }
    public Guid ApplyCaseId { get; private set; }
    public long ApplyApprovalRevision { get; private set; }
    public long ApplySelectedOnboardingVersion { get; private set; }
    public WorkspaceStaffOnboardingProcessingRestrictionState Status
    {
        get;
        private set;
    }
    public long Version { get; private set; } = 1;
    public string AppliedBy { get; private set; } = string.Empty;
    public DateTimeOffset AppliedAtUtc { get; private set; }
    public Guid? ReleaseCaseId { get; private set; }
    public long? ReleaseApprovalRevision { get; private set; }
    public long? ReleaseSelectedOnboardingVersion { get; private set; }
    public string? ReleasedBy { get; private set; }
    public DateTimeOffset? ReleasedAtUtc { get; private set; }

    public static Result<WorkspaceStaffOnboardingProcessingRestriction> Create(
        Guid restrictionId,
        string tenantId,
        Guid applicationId,
        Guid applyCaseId,
        long applyApprovalRevision,
        long applySelectedOnboardingVersion,
        string actorId,
        DateTimeOffset appliedAtUtc)
    {
        if (restrictionId == Guid.Empty ||
            applicationId == Guid.Empty ||
            applyCaseId == Guid.Empty ||
            !TenantIds.TryNormalize(tenantId, out string? scopeId))
        {
            return Result.Failure<
                WorkspaceStaffOnboardingProcessingRestriction>(
                WorkspaceStaffOnboardingProcessingRestrictionErrors
                    .IdentityInvalid);
        }

        if (applyApprovalRevision < 1 ||
            applySelectedOnboardingVersion < 1)
        {
            return Result.Failure<
                WorkspaceStaffOnboardingProcessingRestriction>(
                WorkspaceStaffOnboardingProcessingRestrictionErrors
                    .ApprovalInvalid);
        }

        string? actor = NormalizeActor(actorId);
        if (actor is null || appliedAtUtc == default)
        {
            return Result.Failure<
                WorkspaceStaffOnboardingProcessingRestriction>(
                WorkspaceStaffOnboardingProcessingRestrictionErrors
                    .TransitionInvalid);
        }

        return Result.Success(
            new WorkspaceStaffOnboardingProcessingRestriction(
                restrictionId,
                scopeId)
            {
                ApplicationId = applicationId,
                ApplyCaseId = applyCaseId,
                ApplyApprovalRevision = applyApprovalRevision,
                ApplySelectedOnboardingVersion =
                    applySelectedOnboardingVersion,
                Status =
                    WorkspaceStaffOnboardingProcessingRestrictionState.Active,
                AppliedBy = actor,
                AppliedAtUtc = appliedAtUtc
            });
    }

    public Result Release(
        Guid releaseCaseId,
        long releaseApprovalRevision,
        long releaseSelectedOnboardingVersion,
        long expectedVersion,
        string actorId,
        DateTimeOffset releasedAtUtc)
    {
        Result validation = this.ValidateRelease(
            releaseCaseId,
            releaseApprovalRevision,
            releaseSelectedOnboardingVersion,
            expectedVersion,
            actorId,
            releasedAtUtc);
        if (validation.IsFailure)
        {
            return validation;
        }

        this.ReleaseCaseId = releaseCaseId;
        this.ReleaseApprovalRevision = releaseApprovalRevision;
        this.ReleaseSelectedOnboardingVersion =
            releaseSelectedOnboardingVersion;
        this.ReleasedBy = NormalizeActor(actorId)!;
        this.ReleasedAtUtc = releasedAtUtc;
        this.Status =
            WorkspaceStaffOnboardingProcessingRestrictionState.Released;
        this.Version++;
        return Result.Success();
    }

    public Result ValidateRelease(
        Guid releaseCaseId,
        long releaseApprovalRevision,
        long releaseSelectedOnboardingVersion,
        long expectedVersion,
        string actorId,
        DateTimeOffset releasedAtUtc)
    {
        if (expectedVersion != this.Version)
        {
            return Result.Failure(
                WorkspaceStaffOnboardingProcessingRestrictionErrors
                    .VersionConflict);
        }

        if (this.Status !=
            WorkspaceStaffOnboardingProcessingRestrictionState.Active)
        {
            return Result.Failure(
                WorkspaceStaffOnboardingProcessingRestrictionErrors
                    .AlreadyReleased);
        }

        if (releaseCaseId == Guid.Empty ||
            releaseApprovalRevision < 1 ||
            releaseSelectedOnboardingVersion < 1)
        {
            return Result.Failure(
                WorkspaceStaffOnboardingProcessingRestrictionErrors
                    .ApprovalInvalid);
        }

        string? actor = NormalizeActor(actorId);
        if (actor is null ||
            releasedAtUtc == default ||
            releasedAtUtc < this.AppliedAtUtc)
        {
            return Result.Failure(
                WorkspaceStaffOnboardingProcessingRestrictionErrors
                    .TransitionInvalid);
        }

        return Result.Success();
    }

    private static string? NormalizeActor(string? actorId)
    {
        string? normalized = actorId?.Trim();
        return normalized is { Length: > 0 } &&
            normalized.Length <= WorkspaceStaffOnboardingRules.ActorIdMaxLength
            ? normalized
            : null;
    }
}

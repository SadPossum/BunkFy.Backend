namespace BunkFy.Modules.Workspaces.Domain.DataRights;

using Gma.Framework.Domain;
using Gma.Framework.Naming;
using Gma.Framework.Results;

public sealed class WorkspaceStaffOnboardingProcessingRestrictionProjection
    : IScopedEntity
{
    private WorkspaceStaffOnboardingProcessingRestrictionProjection() { }

    private WorkspaceStaffOnboardingProcessingRestrictionProjection(
        string scopeId,
        Guid applicationId,
        int contractVersion,
        DateTimeOffset initializedAtUtc)
    {
        this.ScopeId = scopeId;
        this.ApplicationId = applicationId;
        this.ContractVersion = contractVersion;
        this.LastTransitionAtUtc = initializedAtUtc;
    }

    public string ScopeId { get; private set; } = string.Empty;
    public Guid ApplicationId { get; private set; }
    public long ProjectionOrdinal { get; private set; }
    public int ContractVersion { get; private set; }
    public long Revision { get; private set; }
    public int ActiveRestrictionCount { get; private set; }
    public bool IsRestricted { get; private set; }
    public DateTimeOffset LastTransitionAtUtc { get; private set; }

    public static Result<
        WorkspaceStaffOnboardingProcessingRestrictionProjection> Create(
        string tenantId,
        Guid applicationId,
        int contractVersion,
        DateTimeOffset initializedAtUtc)
    {
        if (!TenantIds.TryNormalize(tenantId, out string? scopeId) ||
            applicationId == Guid.Empty ||
            contractVersion < 1 ||
            initializedAtUtc == default)
        {
            return Result.Failure<
                WorkspaceStaffOnboardingProcessingRestrictionProjection>(
                WorkspaceStaffOnboardingProcessingRestrictionErrors
                    .ProjectionIdentityInvalid);
        }

        return Result.Success(
            new WorkspaceStaffOnboardingProcessingRestrictionProjection(
                scopeId,
                applicationId,
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
        this.Advance(occurredAtUtc);
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
        this.Advance(occurredAtUtc);
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
            : Result.Failure(
                WorkspaceStaffOnboardingProcessingRestrictionErrors
                    .ProjectionStateInvalid);
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
                WorkspaceStaffOnboardingProcessingRestrictionErrors
                    .ProjectionStateInvalid);
    }

    private Result ValidateTransition(
        long expectedRevision,
        int supportedContractVersion,
        DateTimeOffset occurredAtUtc)
    {
        if (supportedContractVersion < 1 ||
            this.ContractVersion != supportedContractVersion)
        {
            return Result.Failure(
                WorkspaceStaffOnboardingProcessingRestrictionErrors
                    .ProjectionContractUnsupported);
        }

        if (expectedRevision != this.Revision)
        {
            return Result.Failure(
                WorkspaceStaffOnboardingProcessingRestrictionErrors
                    .ProjectionVersionConflict);
        }

        if (occurredAtUtc == default ||
            occurredAtUtc < this.LastTransitionAtUtc)
        {
            return Result.Failure(
                WorkspaceStaffOnboardingProcessingRestrictionErrors
                    .ProjectionTransitionInvalid);
        }

        return Result.Success();
    }

    private void Advance(DateTimeOffset occurredAtUtc)
    {
        this.Revision++;
        this.LastTransitionAtUtc = occurredAtUtc;
    }
}

namespace BunkFy.Modules.Workspaces.Domain;

using Gma.Framework.Results;

public sealed partial class WorkspaceStaffOnboarding
{
    public Result ObserveClaimRequested(Guid claimId, long claimVersion, DateTimeOffset nowUtc)
    {
        Result claim = this.ValidateClaim(claimId, claimVersion);
        if (claim.IsFailure)
        {
            return claim;
        }

        if (this.Status == WorkspaceStaffOnboardingState.Superseded ||
            (this.ClaimVersion.HasValue && claimVersion <= this.ClaimVersion.Value))
        {
            return Result.Success();
        }

        if (this.Status != WorkspaceStaffOnboardingState.Submitted)
        {
            return Result.Failure(WorkspaceStaffOnboardingErrors.StateConflict);
        }

        this.ClaimId = claimId;
        this.ClaimVersion = claimVersion;
        this.Status = WorkspaceStaffOnboardingState.PendingApproval;
        this.Advance(nowUtc);
        return Result.Success();
    }

    public Result ObserveClaimAccepted(Guid claimId, long claimVersion, DateTimeOffset nowUtc)
    {
        Result claim = this.ValidateClaim(claimId, claimVersion);
        if (claim.IsFailure)
        {
            return claim;
        }

        if (this.Status == WorkspaceStaffOnboardingState.Superseded)
        {
            return Result.Success();
        }

        if (this.ClaimVersion.HasValue && claimVersion < this.ClaimVersion.Value)
        {
            return Result.Success();
        }

        if (this.Status == WorkspaceStaffOnboardingState.Rejected)
        {
            return Result.Failure(WorkspaceStaffOnboardingErrors.ClaimConflict);
        }

        bool versionChanged = this.ClaimVersion != claimVersion;
        bool awaitingAcceptance = this.Status is
            WorkspaceStaffOnboardingState.Submitted or
            WorkspaceStaffOnboardingState.PendingApproval;
        this.ClaimId = claimId;
        this.ClaimVersion = claimVersion;

        if (awaitingAcceptance)
        {
            this.Status = WorkspaceStaffOnboardingState.Provisioning;
            this.FailureCode = null;
        }

        if (versionChanged || awaitingAcceptance)
        {
            this.Advance(nowUtc);
        }

        return Result.Success();
    }

    public Result ObserveClaimRejected(Guid claimId, long claimVersion, DateTimeOffset nowUtc)
    {
        Result claim = this.ValidateClaim(claimId, claimVersion);
        if (claim.IsFailure)
        {
            return claim;
        }

        if (this.Status == WorkspaceStaffOnboardingState.Superseded ||
            (this.ClaimVersion.HasValue && claimVersion < this.ClaimVersion.Value))
        {
            return Result.Success();
        }

        if (this.ClaimVersion == claimVersion)
        {
            return this.Status == WorkspaceStaffOnboardingState.Rejected
                ? Result.Success()
                : Result.Failure(WorkspaceStaffOnboardingErrors.ClaimConflict);
        }

        if (this.Status is not (WorkspaceStaffOnboardingState.Submitted or
            WorkspaceStaffOnboardingState.PendingApproval))
        {
            return Result.Failure(WorkspaceStaffOnboardingErrors.StateConflict);
        }

        this.ClaimId = claimId;
        this.ClaimVersion = claimVersion;
        this.Status = WorkspaceStaffOnboardingState.Rejected;
        this.FailureCode = null;
        this.RedactApplicantData();
        this.Advance(nowUtc);
        return Result.Success();
    }

    private Result ValidateClaim(Guid claimId, long claimVersion)
    {
        if (this.SourceKind != WorkspaceStaffOnboardingSource.EnrollmentLink ||
            claimId == Guid.Empty ||
            claimVersion <= 0)
        {
            return Result.Failure(WorkspaceStaffOnboardingErrors.StateConflict);
        }

        if (this.ClaimId.HasValue && this.ClaimId.Value != claimId)
        {
            return Result.Failure(WorkspaceStaffOnboardingErrors.ClaimConflict);
        }

        return Result.Success();
    }
}

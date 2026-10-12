namespace BunkFy.Modules.Workspaces.Domain;

using Gma.Framework.Results;

public sealed partial class WorkspaceStaffOnboarding
{
    public Result BindAcceptedMembership(
        Guid membershipId,
        long membershipVersion,
        DateTimeOffset nowUtc)
    {
        if (membershipId == Guid.Empty || membershipVersion < 1 ||
            this.Status is WorkspaceStaffOnboardingState.Completed or
                WorkspaceStaffOnboardingState.Rejected or
                WorkspaceStaffOnboardingState.Superseded or
                WorkspaceStaffOnboardingState.Expired or
                WorkspaceStaffOnboardingState.Withdrawn)
        {
            return Result.Failure(WorkspaceStaffOnboardingErrors.StateConflict);
        }

        if (this.AcceptedMembershipId.HasValue ||
            this.AcceptedMembershipVersion.HasValue)
        {
            return this.AcceptedMembershipId == membershipId &&
                this.AcceptedMembershipVersion == membershipVersion
                    ? Result.Success()
                    : Result.Failure(
                        WorkspaceStaffOnboardingErrors.StateConflict);
        }

        this.AcceptedMembershipId = membershipId;
        this.AcceptedMembershipVersion = membershipVersion;
        this.Advance(nowUtc);
        return Result.Success();
    }

    public Result RecoverCompletedExact(DateTimeOffset nowUtc)
    {
        if (this.Status == WorkspaceStaffOnboardingState.Completed)
        {
            return Result.Success();
        }

        if (this.Status != WorkspaceStaffOnboardingState.Failed ||
            !this.StaffMemberId.HasValue ||
            !this.AcceptedMembershipId.HasValue ||
            !this.AcceptedMembershipVersion.HasValue)
        {
            return Result.Failure(WorkspaceStaffOnboardingErrors.StateConflict);
        }

        this.Status = WorkspaceStaffOnboardingState.Completed;
        this.FailureCode = null;
        this.RedactApplicantData();
        this.Advance(nowUtc);
        return Result.Success();
    }
}

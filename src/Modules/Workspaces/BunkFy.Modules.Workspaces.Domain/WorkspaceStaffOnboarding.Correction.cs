namespace BunkFy.Modules.Workspaces.Domain;

using Gma.Framework.Results;

public sealed partial class WorkspaceStaffOnboarding
{
    public Result<WorkspaceStaffOnboardingCorrectionOutcome>
        ApplyDataRightsCorrection(
            WorkspaceStaffApplicantProfile profile,
            long expectedVersion,
            Guid applicantEventId,
            DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (expectedVersion != this.Version)
        {
            return Result.Failure<WorkspaceStaffOnboardingCorrectionOutcome>(
                WorkspaceStaffOnboardingErrors.CorrectionVersionConflict);
        }

        if (this.Status != WorkspaceStaffOnboardingState.Submitted)
        {
            return Result.Failure<WorkspaceStaffOnboardingCorrectionOutcome>(
                WorkspaceStaffOnboardingErrors.CorrectionUnavailable);
        }

        if (applicantEventId == Guid.Empty || nowUtc == default)
        {
            return Result.Failure<WorkspaceStaffOnboardingCorrectionOutcome>(
                WorkspaceStaffOnboardingErrors.Invalid);
        }

        WorkspaceStaffOnboardingApplicantField[] changedFields =
            this.GetChangedFields(profile);
        if (changedFields.Length == 0)
        {
            return Result.Failure<WorkspaceStaffOnboardingCorrectionOutcome>(
                WorkspaceStaffOnboardingErrors.CorrectionNoChanges);
        }

        long previousVersion = this.Version;
        this.ApplyProfile(profile);
        this.Advance(nowUtc);
        return Result.Success(
            new WorkspaceStaffOnboardingCorrectionOutcome(
                previousVersion,
                this.Version,
                changedFields,
                applicantEventId,
                nowUtc));
    }

    private WorkspaceStaffOnboardingApplicantField[] GetChangedFields(
        WorkspaceStaffApplicantProfile profile)
    {
        List<WorkspaceStaffOnboardingApplicantField> changed = [];
        AddIfChanged(
            changed,
            WorkspaceStaffOnboardingApplicantField.DisplayName,
            this.DisplayName,
            profile.DisplayName);
        AddIfChanged(
            changed,
            WorkspaceStaffOnboardingApplicantField.LegalName,
            this.LegalName,
            profile.LegalName);
        AddIfChanged(
            changed,
            WorkspaceStaffOnboardingApplicantField.WorkEmail,
            this.WorkEmail,
            profile.WorkEmail);
        AddIfChanged(
            changed,
            WorkspaceStaffOnboardingApplicantField.WorkPhone,
            this.WorkPhone,
            profile.WorkPhone);
        AddIfChanged(
            changed,
            WorkspaceStaffOnboardingApplicantField.EmployeeNumber,
            this.EmployeeNumber,
            profile.EmployeeNumber);
        AddIfChanged(
            changed,
            WorkspaceStaffOnboardingApplicantField.JobTitle,
            this.JobTitle,
            profile.JobTitle);
        AddIfChanged(
            changed,
            WorkspaceStaffOnboardingApplicantField.Department,
            this.Department,
            profile.Department);
        return [.. changed];
    }

    private static void AddIfChanged(
        List<WorkspaceStaffOnboardingApplicantField> changed,
        WorkspaceStaffOnboardingApplicantField field,
        string? current,
        string? requested)
    {
        if (!string.Equals(current, requested, StringComparison.Ordinal))
        {
            changed.Add(field);
        }
    }
}

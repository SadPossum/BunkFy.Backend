namespace BunkFy.Modules.Workspaces.Domain;

using Gma.Framework.Domain.Models;
using Gma.Framework.Naming;
using Gma.Framework.Results;

public sealed partial class WorkspaceStaffOnboarding : ScopedAggregateRoot<Guid>
{
    private WorkspaceStaffOnboarding() { }
    private WorkspaceStaffOnboarding(Guid id, string scopeId) : base(id, scopeId) { }

    public WorkspaceStaffOnboardingSource SourceKind { get; private set; }
    public Guid SourceId { get; private set; }
    public Guid? ClaimId { get; private set; }
    public long? ClaimVersion { get; private set; }
    public string SubjectId { get; private set; } = string.Empty;
    public string? VerifiedAccountEmail { get; private set; }
    public string? DisplayName { get; private set; }
    public string? LegalName { get; private set; }
    public string? WorkEmail { get; private set; }
    public string? WorkPhone { get; private set; }
    public string? EmployeeNumber { get; private set; }
    public string? JobTitle { get; private set; }
    public string? Department { get; private set; }
    public WorkspaceStaffOnboardingState Status { get; private set; }
    public Guid? StaffMemberId { get; private set; }
    public string? FailureCode { get; private set; }
    public long Version { get; private set; } = 1;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset LastChangedAtUtc { get; private set; }

    public bool IsAdmissible => this.Status is
        WorkspaceStaffOnboardingState.Submitted or
        WorkspaceStaffOnboardingState.PendingApproval;

    public bool IsActive => this.Status is not (
        WorkspaceStaffOnboardingState.Completed or
        WorkspaceStaffOnboardingState.Rejected or
        WorkspaceStaffOnboardingState.Superseded or
        WorkspaceStaffOnboardingState.Expired or
        WorkspaceStaffOnboardingState.Withdrawn);

    public bool HasApplicantAuthority =>
        !this.StaffMemberId.HasValue &&
        (this.Status is
            WorkspaceStaffOnboardingState.Submitted or
            WorkspaceStaffOnboardingState.PendingApproval or
            WorkspaceStaffOnboardingState.Provisioning or
            WorkspaceStaffOnboardingState.Failed) &&
        !string.IsNullOrWhiteSpace(this.VerifiedAccountEmail) &&
        !string.IsNullOrWhiteSpace(this.DisplayName);

    public static Result<WorkspaceStaffOnboarding> Create(
        Guid id,
        string scopeId,
        WorkspaceStaffOnboardingSource sourceKind,
        Guid sourceId,
        string subjectId,
        string verifiedAccountEmail,
        string displayName,
        string? legalName,
        string? workEmail,
        string? workPhone,
        string? employeeNumber,
        string? jobTitle,
        string? department,
        DateTimeOffset nowUtc)
    {
        Result<WorkspaceStaffApplicantProfile> profile =
            WorkspaceStaffApplicantProfile.Create(
                displayName,
                legalName,
                workEmail,
                workPhone,
                employeeNumber,
                jobTitle,
                department);
        if (id == Guid.Empty || sourceId == Guid.Empty ||
            sourceKind is not (WorkspaceStaffOnboardingSource.Invitation or
                WorkspaceStaffOnboardingSource.EnrollmentLink) ||
            !TenantIds.TryNormalize(scopeId, out string? normalizedScope) ||
            !TryNormalizeRequired(subjectId, WorkspaceStaffOnboardingRules.SubjectIdMaxLength, out string? subject) ||
            !TryNormalizeRequired(verifiedAccountEmail, WorkspaceStaffOnboardingRules.EmailMaxLength, out string? verifiedEmail) ||
            profile.IsFailure)
        {
            return Result.Failure<WorkspaceStaffOnboarding>(WorkspaceStaffOnboardingErrors.Invalid);
        }

        WorkspaceStaffOnboarding application = new(id, normalizedScope)
        {
            SourceKind = sourceKind,
            SourceId = sourceId,
            SubjectId = subject,
            VerifiedAccountEmail = verifiedEmail,
            Status = WorkspaceStaffOnboardingState.Submitted,
            CreatedAtUtc = nowUtc,
            LastChangedAtUtc = nowUtc
        };
        application.ApplyProfile(profile.Value);
        return Result.Success(application);
    }

    public Result UpdateSubmission(
        string verifiedAccountEmail,
        string displayName,
        string? legalName,
        string? workEmail,
        string? workPhone,
        string? employeeNumber,
        string? jobTitle,
        string? department,
        DateTimeOffset nowUtc)
    {
        if (this.Status != WorkspaceStaffOnboardingState.Submitted)
        {
            return Result.Failure(WorkspaceStaffOnboardingErrors.Unavailable);
        }

        Result<WorkspaceStaffApplicantProfile> profile =
            WorkspaceStaffApplicantProfile.Create(
                displayName,
                legalName,
                workEmail,
                workPhone,
                employeeNumber,
                jobTitle,
                department);
        if (!TryNormalizeRequired(
                verifiedAccountEmail,
                WorkspaceStaffOnboardingRules.EmailMaxLength,
                out string? verifiedEmail) ||
            profile.IsFailure)
        {
            return Result.Failure(WorkspaceStaffOnboardingErrors.Unavailable);
        }

        if (this.MatchesSubmission(verifiedEmail, profile.Value))
        {
            return Result.Success();
        }

        this.VerifiedAccountEmail = verifiedEmail;
        this.ApplyProfile(profile.Value);
        this.Advance(nowUtc);
        return Result.Success();
    }

    public Result ObserveInvitationAccepted(DateTimeOffset nowUtc)
    {
        if (this.SourceKind != WorkspaceStaffOnboardingSource.Invitation)
        {
            return Result.Failure(WorkspaceStaffOnboardingErrors.StateConflict);
        }

        if (this.Status is WorkspaceStaffOnboardingState.Provisioning or
            WorkspaceStaffOnboardingState.StaffReady or
            WorkspaceStaffOnboardingState.Failed or
            WorkspaceStaffOnboardingState.Completed or
            WorkspaceStaffOnboardingState.Superseded or
            WorkspaceStaffOnboardingState.Expired or
            WorkspaceStaffOnboardingState.Withdrawn)
        {
            return Result.Success();
        }

        if (this.Status != WorkspaceStaffOnboardingState.Submitted)
        {
            return Result.Failure(WorkspaceStaffOnboardingErrors.Unavailable);
        }

        this.Status = WorkspaceStaffOnboardingState.Provisioning;
        this.FailureCode = null;
        this.Advance(nowUtc);
        return Result.Success();
    }

    public Result BeginProvisioning(DateTimeOffset nowUtc)
    {
        if (this.Status == WorkspaceStaffOnboardingState.Completed)
        {
            return Result.Success();
        }

        if (this.Status is WorkspaceStaffOnboardingState.Rejected or
            WorkspaceStaffOnboardingState.Superseded or
            WorkspaceStaffOnboardingState.Expired or
            WorkspaceStaffOnboardingState.Withdrawn)
        {
            return Result.Failure(WorkspaceStaffOnboardingErrors.Unavailable);
        }

        if (this.Status is WorkspaceStaffOnboardingState.Submitted or
            WorkspaceStaffOnboardingState.PendingApproval)
        {
            return Result.Failure(WorkspaceStaffOnboardingErrors.StateConflict);
        }

        if (this.Status == WorkspaceStaffOnboardingState.Failed)
        {
            this.Status = WorkspaceStaffOnboardingState.Provisioning;
            this.FailureCode = null;
            this.Advance(nowUtc);
        }

        return this.Status is WorkspaceStaffOnboardingState.Provisioning or
            WorkspaceStaffOnboardingState.StaffReady
                ? Result.Success()
                : Result.Failure(WorkspaceStaffOnboardingErrors.StateConflict);
    }

    public Result MarkStaffReady(Guid staffMemberId, DateTimeOffset nowUtc)
    {
        if (staffMemberId == Guid.Empty || this.Status != WorkspaceStaffOnboardingState.Provisioning)
        {
            return Result.Failure(WorkspaceStaffOnboardingErrors.StateConflict);
        }

        this.StaffMemberId = staffMemberId;
        this.Status = WorkspaceStaffOnboardingState.StaffReady;
        this.Advance(nowUtc);
        return Result.Success();
    }

    public Result Complete(DateTimeOffset nowUtc)
    {
        if (this.Status == WorkspaceStaffOnboardingState.Completed)
        {
            return Result.Success();
        }

        if (this.Status != WorkspaceStaffOnboardingState.StaffReady || !this.StaffMemberId.HasValue)
        {
            return Result.Failure(WorkspaceStaffOnboardingErrors.StateConflict);
        }

        this.Status = WorkspaceStaffOnboardingState.Completed;
        this.FailureCode = null;
        this.RedactApplicantData();
        this.Advance(nowUtc);
        return Result.Success();
    }

    public Result Fail(string failureCode, DateTimeOffset nowUtc)
    {
        if (!TryNormalizeRequired(
                failureCode,
                WorkspaceStaffOnboardingRules.FailureCodeMaxLength,
                out string? normalized))
        {
            return Result.Failure(WorkspaceStaffOnboardingErrors.Invalid);
        }

        if (this.Status is WorkspaceStaffOnboardingState.Completed or
            WorkspaceStaffOnboardingState.Rejected or
            WorkspaceStaffOnboardingState.Superseded or
            WorkspaceStaffOnboardingState.Expired or
            WorkspaceStaffOnboardingState.Withdrawn)
        {
            return Result.Failure(WorkspaceStaffOnboardingErrors.StateConflict);
        }

        this.Status = WorkspaceStaffOnboardingState.Failed;
        this.FailureCode = normalized;
        this.Advance(nowUtc);
        return Result.Success();
    }

    public Result Supersede(DateTimeOffset nowUtc)
    {
        if (this.Status is WorkspaceStaffOnboardingState.Completed or
            WorkspaceStaffOnboardingState.Rejected or
            WorkspaceStaffOnboardingState.Superseded or
            WorkspaceStaffOnboardingState.Expired or
            WorkspaceStaffOnboardingState.Withdrawn)
        {
            return Result.Success();
        }

        this.Status = WorkspaceStaffOnboardingState.Superseded;
        this.FailureCode = null;
        this.RedactApplicantData();
        this.Advance(nowUtc);
        return Result.Success();
    }

    public Result Expire(DateTimeOffset nowUtc)
    {
        if (this.Status is WorkspaceStaffOnboardingState.Completed or
            WorkspaceStaffOnboardingState.Rejected or
            WorkspaceStaffOnboardingState.Superseded or
            WorkspaceStaffOnboardingState.Expired or
            WorkspaceStaffOnboardingState.Withdrawn)
        {
            return Result.Success();
        }

        if (this.Status != WorkspaceStaffOnboardingState.Submitted)
        {
            return Result.Failure(WorkspaceStaffOnboardingErrors.StateConflict);
        }

        this.Status = WorkspaceStaffOnboardingState.Expired;
        this.FailureCode = null;
        this.RedactApplicantData();
        this.Advance(nowUtc);
        return Result.Success();
    }

    private void ApplyProfile(WorkspaceStaffApplicantProfile profile)
    {
        this.DisplayName = profile.DisplayName;
        this.LegalName = profile.LegalName;
        this.WorkEmail = profile.WorkEmail;
        this.WorkPhone = profile.WorkPhone;
        this.EmployeeNumber = profile.EmployeeNumber;
        this.JobTitle = profile.JobTitle;
        this.Department = profile.Department;
    }

    private bool MatchesSubmission(
        string verifiedAccountEmail,
        WorkspaceStaffApplicantProfile profile) =>
        string.Equals(
            this.VerifiedAccountEmail,
            verifiedAccountEmail,
            StringComparison.Ordinal) &&
        string.Equals(this.DisplayName, profile.DisplayName, StringComparison.Ordinal) &&
        string.Equals(this.LegalName, profile.LegalName, StringComparison.Ordinal) &&
        string.Equals(this.WorkEmail, profile.WorkEmail, StringComparison.Ordinal) &&
        string.Equals(this.WorkPhone, profile.WorkPhone, StringComparison.Ordinal) &&
        string.Equals(this.EmployeeNumber, profile.EmployeeNumber, StringComparison.Ordinal) &&
        string.Equals(this.JobTitle, profile.JobTitle, StringComparison.Ordinal) &&
        string.Equals(this.Department, profile.Department, StringComparison.Ordinal);

    private void RedactApplicantData()
    {
        this.VerifiedAccountEmail = null;
        this.DisplayName = null;
        this.LegalName = null;
        this.WorkEmail = null;
        this.WorkPhone = null;
        this.EmployeeNumber = null;
        this.JobTitle = null;
        this.Department = null;
    }

    private void Advance(DateTimeOffset nowUtc)
    {
        this.Version++;
        this.LastChangedAtUtc = nowUtc;
    }

    private static bool TryNormalizeRequired(string? value, int maxLength, out string normalized)
    {
        normalized = value?.Trim() ?? string.Empty;
        return normalized.Length is > 0 && normalized.Length <= maxLength;
    }

}

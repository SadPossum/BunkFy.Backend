namespace BunkFy.Modules.Workspaces.Domain;

using Gma.Framework.Domain.Models;
using Gma.Framework.Naming;
using Gma.Framework.Results;

public sealed class WorkspaceStaffOnboarding : ScopedAggregateRoot<Guid>
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
        if (id == Guid.Empty || sourceId == Guid.Empty ||
            sourceKind is not (WorkspaceStaffOnboardingSource.Invitation or
                WorkspaceStaffOnboardingSource.EnrollmentLink) ||
            !TenantIds.TryNormalize(scopeId, out string? normalizedScope) ||
            !TryNormalizeRequired(subjectId, WorkspaceStaffOnboardingRules.SubjectIdMaxLength, out string? subject) ||
            !TryNormalizeRequired(verifiedAccountEmail, WorkspaceStaffOnboardingRules.EmailMaxLength, out string? verifiedEmail) ||
            !TryNormalizeRequired(displayName, WorkspaceStaffOnboardingRules.DisplayNameMaxLength, out string? name) ||
            !TryNormalizeOptional(legalName, WorkspaceStaffOnboardingRules.LegalNameMaxLength, out string? normalizedLegalName) ||
            !TryNormalizeOptional(workEmail, WorkspaceStaffOnboardingRules.EmailMaxLength, out string? normalizedWorkEmail) ||
            !TryNormalizeOptional(workPhone, WorkspaceStaffOnboardingRules.PhoneMaxLength, out string? normalizedWorkPhone) ||
            !TryNormalizeOptional(employeeNumber, WorkspaceStaffOnboardingRules.EmployeeNumberMaxLength, out string? normalizedEmployeeNumber) ||
            !TryNormalizeOptional(jobTitle, WorkspaceStaffOnboardingRules.JobTitleMaxLength, out string? normalizedJobTitle) ||
            !TryNormalizeOptional(department, WorkspaceStaffOnboardingRules.DepartmentMaxLength, out string? normalizedDepartment))
        {
            return Result.Failure<WorkspaceStaffOnboarding>(WorkspaceStaffOnboardingErrors.Invalid);
        }

        WorkspaceStaffOnboarding application = new(id, normalizedScope)
        {
            SourceKind = sourceKind,
            SourceId = sourceId,
            SubjectId = subject,
            Status = WorkspaceStaffOnboardingState.Submitted,
            CreatedAtUtc = nowUtc,
            LastChangedAtUtc = nowUtc
        };
        application.ApplyProfile(
            verifiedEmail, name, normalizedLegalName, normalizedWorkEmail,
            normalizedWorkPhone, normalizedEmployeeNumber, normalizedJobTitle, normalizedDepartment);
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
        if (!this.IsAdmissible ||
            !TryNormalizeRequired(verifiedAccountEmail, WorkspaceStaffOnboardingRules.EmailMaxLength, out string? verifiedEmail) ||
            !TryNormalizeRequired(displayName, WorkspaceStaffOnboardingRules.DisplayNameMaxLength, out string? name) ||
            !TryNormalizeOptional(legalName, WorkspaceStaffOnboardingRules.LegalNameMaxLength, out string? normalizedLegalName) ||
            !TryNormalizeOptional(workEmail, WorkspaceStaffOnboardingRules.EmailMaxLength, out string? normalizedWorkEmail) ||
            !TryNormalizeOptional(workPhone, WorkspaceStaffOnboardingRules.PhoneMaxLength, out string? normalizedWorkPhone) ||
            !TryNormalizeOptional(employeeNumber, WorkspaceStaffOnboardingRules.EmployeeNumberMaxLength, out string? normalizedEmployeeNumber) ||
            !TryNormalizeOptional(jobTitle, WorkspaceStaffOnboardingRules.JobTitleMaxLength, out string? normalizedJobTitle) ||
            !TryNormalizeOptional(department, WorkspaceStaffOnboardingRules.DepartmentMaxLength, out string? normalizedDepartment))
        {
            return Result.Failure(WorkspaceStaffOnboardingErrors.Unavailable);
        }

        this.ApplyProfile(
            verifiedEmail, name, normalizedLegalName, normalizedWorkEmail,
            normalizedWorkPhone, normalizedEmployeeNumber, normalizedJobTitle, normalizedDepartment);
        this.Advance(nowUtc);
        return Result.Success();
    }

    public Result BindClaim(Guid claimId, long claimVersion, DateTimeOffset nowUtc)
    {
        if (claimId == Guid.Empty || claimVersion <= 0 || !this.IsAdmissible)
        {
            return Result.Failure(WorkspaceStaffOnboardingErrors.StateConflict);
        }

        if (this.ClaimId.HasValue && this.ClaimId.Value != claimId)
        {
            return Result.Failure(WorkspaceStaffOnboardingErrors.ClaimConflict);
        }

        this.ClaimId = claimId;
        this.ClaimVersion = claimVersion;
        this.Status = WorkspaceStaffOnboardingState.PendingApproval;
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
            WorkspaceStaffOnboardingState.Superseded)
        {
            return Result.Failure(WorkspaceStaffOnboardingErrors.Unavailable);
        }

        if (this.Status is not (WorkspaceStaffOnboardingState.Submitted or
            WorkspaceStaffOnboardingState.PendingApproval or
            WorkspaceStaffOnboardingState.Failed or
            WorkspaceStaffOnboardingState.Provisioning or
            WorkspaceStaffOnboardingState.StaffReady))
        {
            return Result.Failure(WorkspaceStaffOnboardingErrors.StateConflict);
        }

        if (this.Status is not (WorkspaceStaffOnboardingState.Provisioning or
            WorkspaceStaffOnboardingState.StaffReady))
        {
            this.Status = WorkspaceStaffOnboardingState.Provisioning;
            this.FailureCode = null;
            this.Advance(nowUtc);
        }

        return Result.Success();
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
            WorkspaceStaffOnboardingState.Superseded)
        {
            return Result.Failure(WorkspaceStaffOnboardingErrors.StateConflict);
        }

        this.Status = WorkspaceStaffOnboardingState.Failed;
        this.FailureCode = normalized;
        this.Advance(nowUtc);
        return Result.Success();
    }

    public Result Reject(long claimVersion, DateTimeOffset nowUtc)
    {
        if (claimVersion <= 0 || this.Status is WorkspaceStaffOnboardingState.Completed or
            WorkspaceStaffOnboardingState.Superseded)
        {
            return Result.Failure(WorkspaceStaffOnboardingErrors.StateConflict);
        }

        this.ClaimVersion = claimVersion;
        this.Status = WorkspaceStaffOnboardingState.Rejected;
        this.FailureCode = null;
        this.RedactApplicantData();
        this.Advance(nowUtc);
        return Result.Success();
    }

    public Result Supersede(DateTimeOffset nowUtc)
    {
        if (this.Status is WorkspaceStaffOnboardingState.Completed or
            WorkspaceStaffOnboardingState.Rejected or
            WorkspaceStaffOnboardingState.Superseded)
        {
            return Result.Success();
        }

        this.Status = WorkspaceStaffOnboardingState.Superseded;
        this.FailureCode = null;
        this.RedactApplicantData();
        this.Advance(nowUtc);
        return Result.Success();
    }

    private void ApplyProfile(
        string verifiedAccountEmail,
        string displayName,
        string? legalName,
        string? workEmail,
        string? workPhone,
        string? employeeNumber,
        string? jobTitle,
        string? department)
    {
        this.VerifiedAccountEmail = verifiedAccountEmail;
        this.DisplayName = displayName;
        this.LegalName = legalName;
        this.WorkEmail = workEmail;
        this.WorkPhone = workPhone;
        this.EmployeeNumber = employeeNumber;
        this.JobTitle = jobTitle;
        this.Department = department;
    }

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

    private static bool TryNormalizeOptional(string? value, int maxLength, out string? normalized)
    {
        normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        return normalized is null || normalized.Length <= maxLength;
    }
}

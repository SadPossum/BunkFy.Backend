namespace BunkFy.Modules.Workspaces.Domain;

using Gma.Framework.Results;

public sealed record WorkspaceStaffApplicantProfile
{
    private WorkspaceStaffApplicantProfile(
        string displayName,
        string? legalName,
        string? workEmail,
        string? workPhone,
        string? employeeNumber,
        string? jobTitle,
        string? department)
    {
        this.DisplayName = displayName;
        this.LegalName = legalName;
        this.WorkEmail = workEmail;
        this.WorkPhone = workPhone;
        this.EmployeeNumber = employeeNumber;
        this.JobTitle = jobTitle;
        this.Department = department;
    }

    public string DisplayName { get; }
    public string? LegalName { get; }
    public string? WorkEmail { get; }
    public string? WorkPhone { get; }
    public string? EmployeeNumber { get; }
    public string? JobTitle { get; }
    public string? Department { get; }

    public static Result<WorkspaceStaffApplicantProfile> Create(
        string? displayName,
        string? legalName,
        string? workEmail,
        string? workPhone,
        string? employeeNumber,
        string? jobTitle,
        string? department)
    {
        if (!TryNormalizeRequired(
                displayName,
                WorkspaceStaffOnboardingRules.DisplayNameMaxLength,
                out string? normalizedDisplayName) ||
            !TryNormalizeOptional(
                legalName,
                WorkspaceStaffOnboardingRules.LegalNameMaxLength,
                out string? normalizedLegalName) ||
            !TryNormalizeOptional(
                workEmail,
                WorkspaceStaffOnboardingRules.EmailMaxLength,
                out string? normalizedWorkEmail) ||
            !TryNormalizeOptional(
                workPhone,
                WorkspaceStaffOnboardingRules.PhoneMaxLength,
                out string? normalizedWorkPhone) ||
            !TryNormalizeOptional(
                employeeNumber,
                WorkspaceStaffOnboardingRules.EmployeeNumberMaxLength,
                out string? normalizedEmployeeNumber) ||
            !TryNormalizeOptional(
                jobTitle,
                WorkspaceStaffOnboardingRules.JobTitleMaxLength,
                out string? normalizedJobTitle) ||
            !TryNormalizeOptional(
                department,
                WorkspaceStaffOnboardingRules.DepartmentMaxLength,
                out string? normalizedDepartment))
        {
            return Result.Failure<WorkspaceStaffApplicantProfile>(
                WorkspaceStaffOnboardingErrors.Invalid);
        }

        return Result.Success(new WorkspaceStaffApplicantProfile(
            normalizedDisplayName,
            normalizedLegalName,
            normalizedWorkEmail,
            normalizedWorkPhone,
            normalizedEmployeeNumber,
            normalizedJobTitle,
            normalizedDepartment));
    }

    private static bool TryNormalizeRequired(
        string? value,
        int maxLength,
        out string normalized)
    {
        normalized = value?.Trim() ?? string.Empty;
        return normalized.Length is > 0 && normalized.Length <= maxLength;
    }

    private static bool TryNormalizeOptional(
        string? value,
        int maxLength,
        out string? normalized)
    {
        normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        return normalized is null || normalized.Length <= maxLength;
    }
}

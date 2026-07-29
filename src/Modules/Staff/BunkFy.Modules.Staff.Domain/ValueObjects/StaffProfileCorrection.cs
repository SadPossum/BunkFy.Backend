namespace BunkFy.Modules.Staff.Domain.ValueObjects;

using Gma.Framework.Results;

public sealed record StaffProfileCorrection
{
    private StaffProfileCorrection(StaffProfile profile)
    {
        this.DisplayName = profile.DisplayName;
        this.LegalName = profile.LegalName;
        this.WorkEmail = profile.WorkEmail;
        this.WorkPhone = profile.WorkPhone;
        this.EmployeeNumber = profile.EmployeeNumber;
        this.JobTitle = profile.JobTitle;
        this.Department = profile.Department;
    }

    public string DisplayName { get; }
    public string? LegalName { get; }
    public string? WorkEmail { get; }
    public string? WorkPhone { get; }
    public string? EmployeeNumber { get; }
    public string? JobTitle { get; }
    public string? Department { get; }

    public static Result<StaffProfileCorrection> Create(
        string? displayName,
        string? legalName,
        string? workEmail,
        string? workPhone,
        string? employeeNumber,
        string? jobTitle,
        string? department)
    {
        Result<StaffProfile> profile = StaffProfile.Create(
            displayName,
            legalName,
            workEmail,
            workPhone,
            employeeNumber,
            jobTitle,
            department,
            authSubjectId: null);
        return profile.IsFailure
            ? Result.Failure<StaffProfileCorrection>(profile.Error)
            : Result.Success(new StaffProfileCorrection(profile.Value));
    }

    internal Result<StaffProfile> ApplyTo(string? authSubjectId) =>
        StaffProfile.Create(
            this.DisplayName,
            this.LegalName,
            this.WorkEmail,
            this.WorkPhone,
            this.EmployeeNumber,
            this.JobTitle,
            this.Department,
            authSubjectId);
}

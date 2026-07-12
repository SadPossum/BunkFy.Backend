namespace BunkFy.Modules.Staff.Domain.ValueObjects;

using System.Net.Mail;
using Gma.Framework.Results;
using BunkFy.Modules.Staff.Domain.Errors;

public sealed record StaffProfile
{
    public const int DisplayNameMaxLength = 256;
    public const int LegalNameMaxLength = 256;
    public const int EmailMaxLength = 320;
    public const int PhoneMaxLength = 64;
    public const int EmployeeNumberMaxLength = 64;
    public const int JobTitleMaxLength = 128;
    public const int DepartmentMaxLength = 128;
    public const int AuthSubjectIdMaxLength = 256;

    private StaffProfile(string displayName, string? legalName, string? workEmail, string? workPhone,
        string? employeeNumber, string? jobTitle, string? department, string? authSubjectId)
    {
        this.DisplayName = displayName;
        this.DisplayNameSearch = displayName.ToUpperInvariant();
        this.LegalName = legalName;
        this.LegalNameSearch = legalName?.ToUpperInvariant();
        this.WorkEmail = workEmail;
        this.WorkEmailSearch = workEmail?.ToUpperInvariant();
        this.WorkPhone = workPhone;
        this.WorkPhoneSearch = workPhone?.ToUpperInvariant();
        this.EmployeeNumber = employeeNumber;
        this.EmployeeNumberSearch = employeeNumber?.ToUpperInvariant();
        this.JobTitle = jobTitle;
        this.Department = department;
        this.AuthSubjectId = authSubjectId;
    }

    public string DisplayName { get; }
    public string DisplayNameSearch { get; }
    public string? LegalName { get; }
    public string? LegalNameSearch { get; }
    public string? WorkEmail { get; }
    public string? WorkEmailSearch { get; }
    public string? WorkPhone { get; }
    public string? WorkPhoneSearch { get; }
    public string? EmployeeNumber { get; }
    public string? EmployeeNumberSearch { get; }
    public string? JobTitle { get; }
    public string? Department { get; }
    public string? AuthSubjectId { get; }

    public static Result<StaffProfile> Create(string? displayName, string? legalName, string? workEmail,
        string? workPhone, string? employeeNumber, string? jobTitle, string? department, string? authSubjectId)
    {
        string name = displayName?.Trim() ?? string.Empty;
        if (name.Length is 0 or > DisplayNameMaxLength)
        {
            return Result.Failure<StaffProfile>(StaffDomainErrors.DisplayNameInvalid);
        }

        string? legal = NormalizeOptional(legalName);
        string? email = NormalizeOptional(workEmail)?.ToLowerInvariant();
        string? phone = NormalizeOptional(workPhone);
        string? employee = NormalizeOptional(employeeNumber);
        string? title = NormalizeOptional(jobTitle);
        string? departmentValue = NormalizeOptional(department);
        bool subjectValid = TryNormalizeAuthSubject(authSubjectId, out string? subject);

        if (legal?.Length > LegalNameMaxLength)
        {
            return Result.Failure<StaffProfile>(StaffDomainErrors.LegalNameInvalid);
        }

        if (email is not null && (email.Length > EmailMaxLength || !MailAddress.TryCreate(email, out _)))
        {
            return Result.Failure<StaffProfile>(StaffDomainErrors.EmailInvalid);
        }

        if (phone?.Length > PhoneMaxLength)
        {
            return Result.Failure<StaffProfile>(StaffDomainErrors.PhoneInvalid);
        }

        if (employee?.Length > EmployeeNumberMaxLength)
        {
            return Result.Failure<StaffProfile>(StaffDomainErrors.EmployeeNumberInvalid);
        }

        if (title?.Length > JobTitleMaxLength)
        {
            return Result.Failure<StaffProfile>(StaffDomainErrors.JobTitleInvalid);
        }

        if (departmentValue?.Length > DepartmentMaxLength)
        {
            return Result.Failure<StaffProfile>(StaffDomainErrors.DepartmentInvalid);
        }

        if (!subjectValid)
        {
            return Result.Failure<StaffProfile>(StaffDomainErrors.AuthSubjectInvalid);
        }

        return Result.Success(new StaffProfile(name, legal, email, phone, employee, title,
            departmentValue, subject));
    }

    internal static bool TryNormalizeAuthSubject(string? value, out string? normalized)
    {
        normalized = NormalizeOptional(value);
        return normalized is null || normalized.Length <= AuthSubjectIdMaxLength;
    }

    internal static string? NormalizeOptional(string? value)
    {
        string normalized = value?.Trim() ?? string.Empty;
        return normalized.Length == 0 ? null : normalized;
    }
}

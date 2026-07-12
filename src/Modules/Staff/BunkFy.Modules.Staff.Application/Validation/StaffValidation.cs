namespace BunkFy.Modules.Staff.Application.Validation;

using BunkFy.Modules.Staff.Contracts;

internal static class StaffValidation
{
    public static IEnumerable<string> Profile(string displayName, string? legalName, string? email,
        string? phone, string? employeeNumber, string? jobTitle, string? department,
        string? authSubjectId, long? version, string actorId)
    {
        if (string.IsNullOrWhiteSpace(displayName) ||
            displayName.Trim().Length > StaffContractLimits.DisplayNameMaxLength)
        {
            yield return "DisplayName is required and must be within the supported limit.";
        }

        if (legalName?.Trim().Length > StaffContractLimits.LegalNameMaxLength ||
            email?.Trim().Length > StaffContractLimits.EmailMaxLength ||
            phone?.Trim().Length > StaffContractLimits.PhoneMaxLength ||
            employeeNumber?.Trim().Length > StaffContractLimits.EmployeeNumberMaxLength ||
            jobTitle?.Trim().Length > StaffContractLimits.JobTitleMaxLength ||
            department?.Trim().Length > StaffContractLimits.DepartmentMaxLength ||
            authSubjectId?.Trim().Length > StaffContractLimits.AuthSubjectIdMaxLength)
        {
            yield return "One or more staff profile fields exceed their supported limits.";
        }

        foreach (string error in Common(version, actorId))
        {
            yield return error;
        }
    }

    public static IEnumerable<string> Common(long? version, string actorId)
    {
        if (version.HasValue && version.Value <= 0)
        {
            yield return "ExpectedVersion must be greater than zero.";
        }

        string actor = actorId?.Trim() ?? string.Empty;
        if (actor.Length is 0 or > StaffContractLimits.ActorIdMaxLength)
        {
            yield return "ActorId is required and must be within the supported limit.";
        }
    }

    public static IEnumerable<string> Reason(string reason)
    {
        string normalized = reason?.Trim() ?? string.Empty;
        if (normalized.Length is 0 or > StaffContractLimits.ReasonMaxLength)
        {
            yield return "Reason is required and must be within the supported limit.";
        }
    }

    public static IEnumerable<string> Lifecycle(Guid staffMemberId, string reason,
        long expectedVersion, string actorId)
    {
        if (staffMemberId == Guid.Empty)
        {
            yield return "StaffMemberId is required.";
        }

        foreach (string error in Reason(reason))
        {
            yield return error;
        }

        foreach (string error in Common(expectedVersion, actorId))
        {
            yield return error;
        }
    }

    public static IEnumerable<string> List(string? search, StaffStatus? status)
    {
        if (search?.Trim().Length > StaffContractLimits.SearchMaxLength)
        {
            yield return "Search exceeds the supported limit.";
        }

        if (status.HasValue && (status.Value == StaffStatus.Unknown || !Enum.IsDefined(status.Value)))
        {
            yield return "Status is invalid.";
        }
    }
}

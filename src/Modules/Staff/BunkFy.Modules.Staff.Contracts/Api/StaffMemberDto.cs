namespace BunkFy.Modules.Staff.Contracts;

public sealed record StaffMemberDto(
    Guid StaffMemberId,
    string DisplayName,
    string? LegalName,
    string? WorkEmail,
    string? WorkPhone,
    string? EmployeeNumber,
    string? JobTitle,
    string? Department,
    string? AuthSubjectId,
    StaffStatus Status,
    long Version,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset LastChangedAtUtc,
    DateTimeOffset? SuspendedAtUtc,
    DateTimeOffset? DepartedAtUtc,
    IReadOnlyList<StaffPropertyAssignmentDto> Assignments);

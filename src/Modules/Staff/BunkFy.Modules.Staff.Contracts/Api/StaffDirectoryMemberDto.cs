namespace BunkFy.Modules.Staff.Contracts;

public sealed record StaffDirectoryMemberDto(
    Guid StaffMemberId,
    string DisplayName,
    string? JobTitle,
    string? Department,
    StaffStatus Status,
    long Version,
    IReadOnlyList<StaffDirectoryAssignmentDto> Assignments);

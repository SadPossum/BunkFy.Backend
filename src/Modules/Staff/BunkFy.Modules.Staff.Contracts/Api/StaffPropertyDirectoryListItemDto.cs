namespace BunkFy.Modules.Staff.Contracts;

public sealed record StaffPropertyDirectoryListItemDto(
    Guid StaffMemberId,
    string DisplayName,
    string? JobTitle,
    string? Department,
    StaffStatus Status,
    long Version,
    StaffDirectoryAssignmentDto Assignment);

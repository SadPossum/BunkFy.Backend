namespace BunkFy.Modules.Staff.Contracts;

public sealed record StaffDirectoryListItemDto(
    Guid StaffMemberId,
    string DisplayName,
    string? JobTitle,
    string? Department,
    StaffStatus Status,
    long Version,
    int CurrentPropertyCount);

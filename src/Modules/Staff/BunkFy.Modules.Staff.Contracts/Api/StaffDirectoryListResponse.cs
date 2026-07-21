namespace BunkFy.Modules.Staff.Contracts;

public sealed record StaffDirectoryListResponse(
    IReadOnlyList<StaffDirectoryMemberDto> Items,
    int Page,
    int PageSize);

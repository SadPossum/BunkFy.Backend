namespace BunkFy.Modules.Staff.Contracts;

public sealed record StaffDirectoryListResponse(
    IReadOnlyList<StaffDirectoryListItemDto> Items,
    int Page,
    int PageSize,
    bool HasMore);

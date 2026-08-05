namespace BunkFy.Modules.Staff.Contracts;

public sealed record StaffPropertyDirectoryListResponse(
    IReadOnlyList<StaffPropertyDirectoryListItemDto> Items,
    int Page,
    int PageSize,
    bool HasMore);

namespace BunkFy.Modules.Staff.Contracts;

public sealed record StaffListResponse(IReadOnlyList<StaffMemberDto> Items, int Page, int PageSize);

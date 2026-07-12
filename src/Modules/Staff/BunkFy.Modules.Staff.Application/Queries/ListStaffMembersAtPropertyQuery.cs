namespace BunkFy.Modules.Staff.Application.Queries;

using Gma.Framework.Cqrs;
using BunkFy.Modules.Staff.Contracts;

public sealed record ListStaffMembersAtPropertyQuery(Guid PropertyId, string? Search,
    StaffStatus? Status, int Page, int PageSize) : IQuery<StaffListResponse>;

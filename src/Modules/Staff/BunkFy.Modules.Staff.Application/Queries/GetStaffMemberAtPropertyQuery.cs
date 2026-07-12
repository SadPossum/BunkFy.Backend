namespace BunkFy.Modules.Staff.Application.Queries;

using Gma.Framework.Cqrs;
using BunkFy.Modules.Staff.Contracts;

public sealed record GetStaffMemberAtPropertyQuery(Guid PropertyId, Guid StaffMemberId)
    : IQuery<StaffMemberDto>;

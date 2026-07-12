namespace BunkFy.Modules.Staff.Application.Queries;

using Gma.Framework.Cqrs;
using BunkFy.Modules.Staff.Contracts;

public sealed record GetStaffMemberQuery(Guid StaffMemberId) : IQuery<StaffMemberDto>;

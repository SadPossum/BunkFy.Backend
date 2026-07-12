namespace BunkFy.Modules.Staff.Application.Validation;

using Gma.Framework.Cqrs;
using BunkFy.Modules.Staff.Application.Queries;

internal sealed class GetStaffMemberQueryValidator : IQueryValidator<GetStaffMemberQuery>
{
    public IEnumerable<string> Validate(GetStaffMemberQuery query)
    {
        if (query.StaffMemberId == Guid.Empty)
        {
            yield return "StaffMemberId is required.";
        }
    }
}

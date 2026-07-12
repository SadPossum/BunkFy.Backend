namespace BunkFy.Modules.Staff.Application.Validation;

using Gma.Framework.Cqrs;
using BunkFy.Modules.Staff.Application.Queries;

internal sealed class GetStaffMemberAtPropertyQueryValidator : IQueryValidator<GetStaffMemberAtPropertyQuery>
{
    public IEnumerable<string> Validate(GetStaffMemberAtPropertyQuery query)
    {
        if (query.StaffMemberId == Guid.Empty || query.PropertyId == Guid.Empty)
        {
            yield return "StaffMemberId and PropertyId are required.";
        }
    }
}

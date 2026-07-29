namespace BunkFy.Modules.Staff.Application.Validation;

using BunkFy.Modules.Staff.Application.Queries;
using Gma.Framework.Cqrs;

internal sealed class GetStaffEmploymentGovernanceQueryValidator
    : IQueryValidator<GetStaffEmploymentGovernanceQuery>
{
    public IEnumerable<string> Validate(
        GetStaffEmploymentGovernanceQuery query)
    {
        if (query.StaffMemberId == Guid.Empty)
        {
            yield return "StaffMemberId is required.";
        }
    }
}

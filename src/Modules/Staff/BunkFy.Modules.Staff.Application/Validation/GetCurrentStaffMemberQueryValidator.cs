namespace BunkFy.Modules.Staff.Application.Validation;

using BunkFy.Modules.Staff.Application.Queries;
using BunkFy.Modules.Staff.Contracts;
using Gma.Framework.Cqrs;

internal sealed class GetCurrentStaffMemberQueryValidator : IQueryValidator<GetCurrentStaffMemberQuery>
{
    public IEnumerable<string> Validate(GetCurrentStaffMemberQuery query)
    {
        string subjectId = query.AuthSubjectId?.Trim() ?? string.Empty;
        if (subjectId.Length is 0 or > StaffContractLimits.AuthSubjectIdMaxLength)
        {
            yield return "AuthSubjectId is required and must be within the supported limit.";
        }
    }
}

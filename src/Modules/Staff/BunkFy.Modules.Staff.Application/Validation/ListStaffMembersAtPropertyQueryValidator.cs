namespace BunkFy.Modules.Staff.Application.Validation;

using Gma.Framework.Cqrs;
using BunkFy.Modules.Staff.Application.Queries;

internal sealed class ListStaffMembersAtPropertyQueryValidator
    : IQueryValidator<ListStaffMembersAtPropertyQuery>
{
    public IEnumerable<string> Validate(ListStaffMembersAtPropertyQuery query)
    {
        if (query.PropertyId == Guid.Empty)
        {
            yield return "PropertyId is required.";
        }

        foreach (string error in StaffValidation.List(query.Search, query.Status))
        {
            yield return error;
        }
    }
}

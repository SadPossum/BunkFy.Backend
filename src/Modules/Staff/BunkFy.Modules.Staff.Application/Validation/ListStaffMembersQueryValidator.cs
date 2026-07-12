namespace BunkFy.Modules.Staff.Application.Validation;

using Gma.Framework.Cqrs;
using BunkFy.Modules.Staff.Application.Queries;

internal sealed class ListStaffMembersQueryValidator : IQueryValidator<ListStaffMembersQuery>
{
    public IEnumerable<string> Validate(ListStaffMembersQuery query) =>
        StaffValidation.List(query.Search, query.Status);
}

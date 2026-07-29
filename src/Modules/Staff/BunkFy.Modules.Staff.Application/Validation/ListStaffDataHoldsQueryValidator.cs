namespace BunkFy.Modules.Staff.Application.Validation;

using BunkFy.Modules.Staff.Application.Queries;
using BunkFy.Modules.Staff.Contracts;
using Gma.Framework.Cqrs;

internal sealed class ListStaffDataHoldsQueryValidator
    : IQueryValidator<ListStaffDataHoldsQuery>
{
    public IEnumerable<string> Validate(
        ListStaffDataHoldsQuery query)
    {
        if (query.StaffMemberId == Guid.Empty)
        {
            yield return "StaffMemberId is required.";
        }

        if (query.Status.HasValue &&
            query.Status is not StaffDataHoldStatus.Active and
                not StaffDataHoldStatus.Released)
        {
            yield return "Status is invalid.";
        }
    }
}

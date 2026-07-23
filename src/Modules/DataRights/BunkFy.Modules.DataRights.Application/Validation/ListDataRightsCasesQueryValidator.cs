namespace BunkFy.Modules.DataRights.Application.Validation;

using BunkFy.Modules.DataRights.Application.Queries;
using BunkFy.Modules.DataRights.Contracts;
using Gma.Framework.Cqrs;

internal sealed class ListDataRightsCasesQueryValidator : IQueryValidator<ListDataRightsCasesQuery>
{
    public IEnumerable<string> Validate(ListDataRightsCasesQuery query)
    {
        if (query.PropertyId == Guid.Empty)
        {
            yield return "PropertyId is required.";
        }

        if (query.Status.HasValue &&
            (query.Status.Value == DataRightsCaseStatus.Unknown ||
             !Enum.IsDefined(query.Status.Value)))
        {
            yield return "Status is invalid.";
        }
    }
}

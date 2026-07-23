namespace BunkFy.Modules.DataRights.Application.Validation;

using BunkFy.Modules.DataRights.Application.Queries;
using Gma.Framework.Cqrs;

internal sealed class GetDataRightsCaseQueryValidator : IQueryValidator<GetDataRightsCaseQuery>
{
    public IEnumerable<string> Validate(GetDataRightsCaseQuery query)
    {
        if (query.PropertyId == Guid.Empty || query.CaseId == Guid.Empty)
        {
            yield return "PropertyId and CaseId are required.";
        }
    }
}

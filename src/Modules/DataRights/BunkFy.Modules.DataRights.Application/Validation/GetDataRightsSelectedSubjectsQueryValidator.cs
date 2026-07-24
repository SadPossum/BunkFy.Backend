namespace BunkFy.Modules.DataRights.Application.Validation;

using BunkFy.Modules.DataRights.Application.Queries;
using Gma.Framework.Cqrs;

internal sealed class GetDataRightsSelectedSubjectsQueryValidator
    : IQueryValidator<GetDataRightsSelectedSubjectsQuery>
{
    public IEnumerable<string> Validate(GetDataRightsSelectedSubjectsQuery query)
    {
        if (query.PropertyId == Guid.Empty || query.CaseId == Guid.Empty)
        {
            yield return "PropertyId and CaseId are required.";
        }
    }
}

namespace BunkFy.Modules.DataRights.Application.Validation;

using BunkFy.Modules.DataRights.Application.Queries;
using Gma.Framework.Cqrs;

internal sealed class GetDataRightsSelectedSubjectsQueryValidator
    : IQueryValidator<GetDataRightsSelectedSubjectsQuery>
{
    public IEnumerable<string> Validate(GetDataRightsSelectedSubjectsQuery query)
    {
        if (query.Scope is null || query.CaseId == Guid.Empty)
        {
            yield return "Scope and CaseId are required.";
        }
    }
}

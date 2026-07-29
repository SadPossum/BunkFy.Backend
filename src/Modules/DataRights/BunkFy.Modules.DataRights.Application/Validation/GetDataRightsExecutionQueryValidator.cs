namespace BunkFy.Modules.DataRights.Application.Validation;

using BunkFy.Modules.DataRights.Application.Queries;
using Gma.Framework.Cqrs;

internal sealed class GetDataRightsExecutionQueryValidator
    : IQueryValidator<GetDataRightsExecutionQuery>
{
    public IEnumerable<string> Validate(GetDataRightsExecutionQuery query)
    {
        if (query.Scope is null)
        {
            yield return "Scope is required.";
        }

        if (query.CaseId == Guid.Empty)
        {
            yield return "CaseId is required.";
        }
    }
}

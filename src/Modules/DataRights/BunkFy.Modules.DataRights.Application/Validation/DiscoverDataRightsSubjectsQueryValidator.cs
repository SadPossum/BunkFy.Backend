namespace BunkFy.Modules.DataRights.Application.Validation;

using BunkFy.Modules.DataRights.Application.Queries;
using Gma.Framework.Cqrs;

internal sealed class DiscoverDataRightsSubjectsQueryValidator
    : IQueryValidator<DiscoverDataRightsSubjectsQuery>
{
    public IEnumerable<string> Validate(DiscoverDataRightsSubjectsQuery query)
    {
        if (query.PropertyId == Guid.Empty || query.CaseId == Guid.Empty)
        {
            yield return "PropertyId and CaseId are required.";
        }

        foreach (string error in DataRightsSubjectLookupPolicy.Validate(query.Lookup))
        {
            yield return error;
        }
    }
}

namespace BunkFy.Modules.DataRights.Application.Validation;

using BunkFy.Modules.DataRights.Application.Queries;
using BunkFy.Modules.DataRights.Contracts;
using Gma.Framework.Cqrs;

internal sealed class DiscoverDataRightsSubjectsQueryValidator
    : IQueryValidator<DiscoverDataRightsSubjectsQuery>
{
    public IEnumerable<string> Validate(DiscoverDataRightsSubjectsQuery query)
    {
        if (query.Scope is null || query.CaseId == Guid.Empty)
        {
            yield return "Scope and CaseId are required.";
        }

        if (query.OwnerKey is not null &&
            (string.IsNullOrWhiteSpace(query.OwnerKey) ||
                query.OwnerKey.Trim().Length > DataRightsSubjectDiscoveryLimits.OwnerKeyMaxLength))
        {
            yield return $"OwnerKey must contain between 1 and " +
                $"{DataRightsSubjectDiscoveryLimits.OwnerKeyMaxLength} characters when supplied.";
        }

        foreach (string error in DataRightsSubjectLookupPolicy.Validate(query.Lookup))
        {
            yield return error;
        }
    }
}

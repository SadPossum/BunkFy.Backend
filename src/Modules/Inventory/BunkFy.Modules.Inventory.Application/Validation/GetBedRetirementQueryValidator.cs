namespace BunkFy.Modules.Inventory.Application.Validation;

using BunkFy.Modules.Inventory.Application.Queries;
using Gma.Framework.Cqrs;

internal sealed class GetBedRetirementQueryValidator : IQueryValidator<GetBedRetirementQuery>
{
    public IEnumerable<string> Validate(GetBedRetirementQuery query)
    {
        if (query.PropertyId == Guid.Empty || query.TopologyChangeId == Guid.Empty)
        {
            yield return "PropertyId and TopologyChangeId are required.";
        }
    }
}

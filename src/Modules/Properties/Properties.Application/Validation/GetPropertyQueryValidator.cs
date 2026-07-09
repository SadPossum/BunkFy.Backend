namespace Properties.Application.Validation;

using Properties.Application.Queries;
using Gma.Framework.Cqrs;

internal sealed class GetPropertyQueryValidator : IQueryValidator<GetPropertyQuery>
{
    public IEnumerable<string> Validate(GetPropertyQuery query)
    {
        if (query.PropertyId == Guid.Empty)
        {
            yield return "Property id is required.";
        }
    }
}

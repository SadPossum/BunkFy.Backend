namespace Properties.Application.Validation;

using Properties.Application.Queries;
using Gma.Framework.Cqrs;

internal sealed class ListRoomsQueryValidator : IQueryValidator<ListRoomsQuery>
{
    public IEnumerable<string> Validate(ListRoomsQuery query)
    {
        if (query.PropertyId == Guid.Empty)
        {
            yield return "Property id is required.";
        }
    }
}

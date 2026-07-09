namespace Properties.Application.Validation;

using Properties.Application.Queries;
using Gma.Framework.Cqrs;

internal sealed class ListBedsQueryValidator : IQueryValidator<ListBedsQuery>
{
    public IEnumerable<string> Validate(ListBedsQuery query)
    {
        if (query.RoomId == Guid.Empty)
        {
            yield return "Room id is required.";
        }
    }
}

namespace BunkFy.Modules.Properties.Application.Validation;

using BunkFy.Modules.Properties.Application.Queries;
using Gma.Framework.Cqrs;

internal sealed class GetRoomQueryValidator : IQueryValidator<GetRoomQuery>
{
    public IEnumerable<string> Validate(GetRoomQuery query)
    {
        if (query.RoomId == Guid.Empty)
        {
            yield return "Room id is required.";
        }
    }
}

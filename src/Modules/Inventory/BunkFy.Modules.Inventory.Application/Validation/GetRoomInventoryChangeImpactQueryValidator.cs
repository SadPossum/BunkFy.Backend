namespace BunkFy.Modules.Inventory.Application.Validation;

using BunkFy.Modules.Inventory.Application.Queries;
using Gma.Framework.Cqrs;

internal sealed class GetRoomInventoryChangeImpactQueryValidator
    : IQueryValidator<GetRoomInventoryChangeImpactQuery>
{
    public IEnumerable<string> Validate(GetRoomInventoryChangeImpactQuery query)
    {
        if (query.PropertyId == Guid.Empty)
        {
            yield return "PropertyId is required.";
        }

        if (query.RoomId == Guid.Empty)
        {
            yield return "RoomId is required.";
        }
    }
}

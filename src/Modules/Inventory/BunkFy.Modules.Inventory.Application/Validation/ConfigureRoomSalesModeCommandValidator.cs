namespace BunkFy.Modules.Inventory.Application.Validation;

using BunkFy.Modules.Inventory.Application.Commands;
using BunkFy.Modules.Inventory.Contracts;
using Gma.Framework.Cqrs;

internal sealed class ConfigureRoomSalesModeCommandValidator : ICommandValidator<ConfigureRoomSalesModeCommand>
{
    public IEnumerable<string> Validate(ConfigureRoomSalesModeCommand command)
    {
        if (command.OperationId == Guid.Empty)
        {
            yield return "Operation id is required.";
        }

        if (command.PropertyId == Guid.Empty)
        {
            yield return "Property id is required.";
        }

        if (command.RoomId == Guid.Empty)
        {
            yield return "Room id is required.";
        }

        if (command.SalesMode is not (InventorySalesMode.RoomLevel or InventorySalesMode.BedLevel))
        {
            yield return "Sales mode must be room-level or bed-level.";
        }

        if (command.ExpectedVersion <= 0)
        {
            yield return "Expected version must be greater than zero.";
        }
    }
}

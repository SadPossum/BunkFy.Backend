namespace BunkFy.Modules.Inventory.Application.Validation;

using BunkFy.Modules.Inventory.Application.Commands;
using BunkFy.Modules.Inventory.Contracts;
using BunkFy.Modules.Inventory.Domain.Aggregates;
using Gma.Framework.Cqrs;

internal sealed class CreateManualInventoryBlockGroupCommandValidator
    : ICommandValidator<CreateManualInventoryBlockGroupCommand>
{
    public IEnumerable<string> Validate(CreateManualInventoryBlockGroupCommand command)
    {
        if (command.PropertyId == Guid.Empty)
        {
            yield return "PropertyId is required.";
        }

        if (!IsValidTarget(command.Target))
        {
            yield return "Target must identify a property, building, floor, room, or inventory unit.";
        }

        if (command.Arrival >= command.Departure)
        {
            yield return "Arrival must be before Departure.";
        }

        if (string.IsNullOrWhiteSpace(command.Reason) ||
            command.Reason.Trim().Length > ManualInventoryBlock.ReasonMaxLength)
        {
            yield return $"Reason is required and must be {ManualInventoryBlock.ReasonMaxLength} characters or fewer.";
        }
    }

    private static bool IsValidTarget(InventoryBlockTarget? target) => target?.Kind switch
    {
        InventoryBlockTargetKind.Property => true,
        InventoryBlockTargetKind.Building => !string.IsNullOrWhiteSpace(target.BuildingLabel),
        InventoryBlockTargetKind.Floor => !string.IsNullOrWhiteSpace(target.FloorLabel),
        InventoryBlockTargetKind.Room => target.RoomId is { } roomId && roomId != Guid.Empty,
        InventoryBlockTargetKind.Unit => target.InventoryUnitId is { } unitId && unitId != Guid.Empty,
        _ => false
    };
}

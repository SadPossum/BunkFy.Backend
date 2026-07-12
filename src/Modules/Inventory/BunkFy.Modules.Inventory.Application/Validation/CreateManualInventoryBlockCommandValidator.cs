namespace BunkFy.Modules.Inventory.Application.Validation;

using Gma.Framework.Cqrs;
using BunkFy.Modules.Inventory.Application.Commands;
using BunkFy.Modules.Inventory.Domain.Aggregates;

internal sealed class CreateManualInventoryBlockCommandValidator : ICommandValidator<CreateManualInventoryBlockCommand>
{
    public IEnumerable<string> Validate(CreateManualInventoryBlockCommand command)
    {
        if (command.PropertyId == Guid.Empty)
        {
            yield return "PropertyId is required.";
        }

        if (command.InventoryUnitId == Guid.Empty)
        {
            yield return "InventoryUnitId is required.";
        }

        if (command.Arrival >= command.Departure)
        {
            yield return "Arrival must be before Departure.";
        }

        if (string.IsNullOrWhiteSpace(command.Reason) || command.Reason.Trim().Length > ManualInventoryBlock.ReasonMaxLength)
        {
            yield return $"Reason is required and must be {ManualInventoryBlock.ReasonMaxLength} characters or fewer.";
        }
    }
}

namespace BunkFy.Modules.Inventory.Application.Validation;

using Gma.Framework.Cqrs;
using BunkFy.Modules.Inventory.Application.Commands;

internal sealed class ReleaseManualInventoryBlockCommandValidator : ICommandValidator<ReleaseManualInventoryBlockCommand>
{
    public IEnumerable<string> Validate(ReleaseManualInventoryBlockCommand command)
    {
        if (command.PropertyId == Guid.Empty)
        {
            yield return "PropertyId is required.";
        }

        if (command.BlockId == Guid.Empty)
        {
            yield return "BlockId is required.";
        }

        if (command.ExpectedVersion <= 0)
        {
            yield return "ExpectedVersion must be greater than zero.";
        }
    }
}

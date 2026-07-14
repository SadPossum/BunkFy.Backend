namespace BunkFy.Modules.Inventory.Application.Validation;

using BunkFy.Modules.Inventory.Application.Commands;
using Gma.Framework.Cqrs;

internal sealed class ReleaseManualInventoryBlockGroupCommandValidator
    : ICommandValidator<ReleaseManualInventoryBlockGroupCommand>
{
    public IEnumerable<string> Validate(ReleaseManualInventoryBlockGroupCommand command)
    {
        if (command.PropertyId == Guid.Empty)
        {
            yield return "PropertyId is required.";
        }

        if (command.BlockGroupId == Guid.Empty)
        {
            yield return "BlockGroupId is required.";
        }
    }
}

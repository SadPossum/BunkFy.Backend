namespace BunkFy.Modules.Inventory.Application.Validation;

using BunkFy.Modules.Inventory.Application.Commands;
using Gma.Framework.Cqrs;

internal sealed class RetryRoomRetirementCommandValidator : ICommandValidator<RetryRoomRetirementCommand>
{
    public IEnumerable<string> Validate(RetryRoomRetirementCommand command)
    {
        if (command.PropertyId == Guid.Empty || command.TopologyChangeId == Guid.Empty)
        {
            yield return "PropertyId and TopologyChangeId are required.";
        }
    }
}

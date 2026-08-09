namespace BunkFy.Modules.Inventory.Application.Validation;

using BunkFy.Modules.Inventory.Application.Commands;
using Gma.Framework.Cqrs;

internal sealed class RetryRoomRetirementCommandValidator : ICommandValidator<RetryRoomRetirementCommand>
{
    public IEnumerable<string> Validate(RetryRoomRetirementCommand command)
    {
        if (command.OperationId == Guid.Empty ||
            command.PropertyId == Guid.Empty ||
            command.TopologyChangeId == Guid.Empty)
        {
            yield return "OperationId, PropertyId and TopologyChangeId are required.";
        }

        if (command.ExpectedVersion <= 0)
        {
            yield return "ExpectedVersion must be greater than zero.";
        }
    }
}

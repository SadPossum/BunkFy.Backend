namespace BunkFy.Modules.Inventory.Application.Validation;

using BunkFy.Modules.Inventory.Application.Commands;
using Gma.Framework.Cqrs;

internal sealed class RetryBedRetirementCommandValidator : ICommandValidator<RetryBedRetirementCommand>
{
    public IEnumerable<string> Validate(RetryBedRetirementCommand command)
    {
        if (command.PropertyId == Guid.Empty || command.TopologyChangeId == Guid.Empty)
        {
            yield return "PropertyId and TopologyChangeId are required.";
        }
    }
}

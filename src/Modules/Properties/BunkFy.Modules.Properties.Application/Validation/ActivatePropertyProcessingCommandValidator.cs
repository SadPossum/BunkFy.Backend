namespace BunkFy.Modules.Properties.Application.Validation;

using BunkFy.Modules.Properties.Application.Commands;
using Gma.Framework.Cqrs;

internal sealed class ActivatePropertyProcessingCommandValidator
    : ICommandValidator<ActivatePropertyProcessingCommand>
{
    public IEnumerable<string> Validate(
        ActivatePropertyProcessingCommand command)
    {
        if (command.PropertyId == Guid.Empty)
        {
            yield return "Property id is required.";
        }

        if (command.OperationId == Guid.Empty)
        {
            yield return "OperationId is required.";
        }

        foreach (string error in PropertiesValidation.ValidateExpectedVersion(
                     command.ExpectedVersion,
                     "property"))
        {
            yield return error;
        }

        foreach (string error in PropertiesValidation.ValidateActor(
                     command.ActorId,
                     required: true))
        {
            yield return error;
        }
    }
}

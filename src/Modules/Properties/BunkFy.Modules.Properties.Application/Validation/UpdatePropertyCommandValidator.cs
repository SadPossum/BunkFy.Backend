namespace BunkFy.Modules.Properties.Application.Validation;

using BunkFy.Modules.Properties.Application.Commands;
using Gma.Framework.Cqrs;

internal sealed class UpdatePropertyCommandValidator : ICommandValidator<UpdatePropertyCommand>
{
    public IEnumerable<string> Validate(UpdatePropertyCommand command)
    {
        if (command.PropertyId == Guid.Empty)
        {
            yield return "Property id is required.";
        }

        foreach (string error in PropertiesValidation.ValidateExpectedVersion(command.ExpectedVersion, "property"))
        {
            yield return error;
        }

        foreach (string error in PropertiesValidation.ValidatePropertyWrite(command.Name, command.Code, command.TimeZoneId))
        {
            yield return error;
        }
    }
}

namespace Properties.Application.Validation;

using Properties.Application.Commands;
using Gma.Framework.Cqrs;

internal sealed class CreateRoomCommandValidator : ICommandValidator<CreateRoomCommand>
{
    public IEnumerable<string> Validate(CreateRoomCommand command)
    {
        if (command.PropertyId == Guid.Empty)
        {
            yield return "Property id is required.";
        }

        foreach (string error in PropertiesValidation.ValidateExpectedVersion(command.ExpectedPropertyVersion, "property"))
        {
            yield return error;
        }

        foreach (string error in PropertiesValidation.ValidateRoomWrite(command.Name, command.BuildingLabel, command.FloorLabel))
        {
            yield return error;
        }
    }
}

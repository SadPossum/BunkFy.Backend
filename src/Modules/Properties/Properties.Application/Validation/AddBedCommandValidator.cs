namespace Properties.Application.Validation;

using Properties.Application.Commands;
using Gma.Framework.Cqrs;

internal sealed class AddBedCommandValidator : ICommandValidator<AddBedCommand>
{
    public IEnumerable<string> Validate(AddBedCommand command)
    {
        if (command.PropertyId == Guid.Empty)
        {
            yield return "Property id is required.";
        }

        if (command.RoomId == Guid.Empty)
        {
            yield return "Room id is required.";
        }

        foreach (string error in PropertiesValidation.ValidateExpectedVersion(command.ExpectedRoomVersion, "room"))
        {
            yield return error;
        }

        foreach (string error in PropertiesValidation.ValidateBedWrite(command.Label))
        {
            yield return error;
        }
    }
}

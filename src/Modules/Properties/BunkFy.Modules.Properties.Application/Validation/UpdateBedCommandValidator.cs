namespace BunkFy.Modules.Properties.Application.Validation;

using BunkFy.Modules.Properties.Application.Commands;
using Gma.Framework.Cqrs;

internal sealed class UpdateBedCommandValidator : ICommandValidator<UpdateBedCommand>
{
    public IEnumerable<string> Validate(UpdateBedCommand command)
    {
        if (command.PropertyId == Guid.Empty)
        {
            yield return "Property id is required.";
        }

        if (command.RoomId == Guid.Empty)
        {
            yield return "Room id is required.";
        }

        if (command.BedId == Guid.Empty)
        {
            yield return "Bed id is required.";
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

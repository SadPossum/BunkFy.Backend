namespace Properties.Application.Validation;

using Properties.Application.Commands;
using Gma.Framework.Cqrs;

internal sealed class RetireBedCommandValidator : ICommandValidator<RetireBedCommand>
{
    public IEnumerable<string> Validate(RetireBedCommand command)
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
    }
}

namespace Properties.Application.Validation;

using Properties.Application.Commands;
using Gma.Framework.Cqrs;

internal sealed class RetireRoomCommandValidator : ICommandValidator<RetireRoomCommand>
{
    public IEnumerable<string> Validate(RetireRoomCommand command)
    {
        if (command.PropertyId == Guid.Empty)
        {
            yield return "Property id is required.";
        }

        if (command.RoomId == Guid.Empty)
        {
            yield return "Room id is required.";
        }


        foreach (string error in PropertiesValidation.ValidateExpectedVersion(command.ExpectedVersion, "room"))
        {
            yield return error;
        }
    }
}

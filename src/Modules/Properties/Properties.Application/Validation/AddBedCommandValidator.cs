namespace Properties.Application.Validation;

using Properties.Application.Commands;
using Gma.Framework.Cqrs;

internal sealed class AddBedCommandValidator : ICommandValidator<AddBedCommand>
{
    public IEnumerable<string> Validate(AddBedCommand command)
    {
        if (command.RoomId == Guid.Empty)
        {
            yield return "Room id is required.";
        }

        foreach (string error in PropertiesValidation.ValidateBedWrite(command.Label))
        {
            yield return error;
        }
    }
}

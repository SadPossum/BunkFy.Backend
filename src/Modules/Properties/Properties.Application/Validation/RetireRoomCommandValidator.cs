namespace Properties.Application.Validation;

using Properties.Application.Commands;
using Gma.Framework.Cqrs;

internal sealed class RetireRoomCommandValidator : ICommandValidator<RetireRoomCommand>
{
    public IEnumerable<string> Validate(RetireRoomCommand command)
    {
        if (command.RoomId == Guid.Empty)
        {
            yield return "Room id is required.";
        }
    }
}

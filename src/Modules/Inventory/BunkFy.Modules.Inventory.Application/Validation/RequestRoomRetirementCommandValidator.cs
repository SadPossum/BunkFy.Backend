namespace BunkFy.Modules.Inventory.Application.Validation;

using BunkFy.Modules.Inventory.Application.Commands;
using BunkFy.Modules.Inventory.Domain.Aggregates;
using Gma.Framework.Cqrs;

internal sealed class RequestRoomRetirementCommandValidator : ICommandValidator<RequestRoomRetirementCommand>
{
    public IEnumerable<string> Validate(RequestRoomRetirementCommand command)
    {
        if (command.PropertyId == Guid.Empty || command.RoomId == Guid.Empty)
        {
            yield return "PropertyId and RoomId are required.";
        }

        if (string.IsNullOrWhiteSpace(command.Reason) || command.Reason.Trim().Length > RoomRetirementProcess.ReasonMaxLength)
        {
            yield return "Reason is required and is too long.";
        }

        if (string.IsNullOrWhiteSpace(command.RequestedBy) || command.RequestedBy.Trim().Length > RoomRetirementProcess.ActorIdMaxLength)
        {
            yield return "RequestedBy is required and is too long.";
        }
    }
}

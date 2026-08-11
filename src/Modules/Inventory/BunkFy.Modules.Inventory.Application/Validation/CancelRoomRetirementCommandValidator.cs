namespace BunkFy.Modules.Inventory.Application.Validation;

using BunkFy.Modules.Inventory.Application.Commands;
using BunkFy.Modules.Inventory.Domain.Aggregates;
using Gma.Framework.Cqrs;

internal sealed class CancelRoomRetirementCommandValidator : ICommandValidator<CancelRoomRetirementCommand>
{
    public IEnumerable<string> Validate(CancelRoomRetirementCommand command)
    {
        if (command.OperationId == Guid.Empty ||
            command.PropertyId == Guid.Empty ||
            command.TopologyChangeId == Guid.Empty)
        {
            yield return "OperationId, PropertyId and TopologyChangeId are required.";
        }

        if (command.ExpectedVersion <= 0)
        {
            yield return "ExpectedVersion must be greater than zero.";
        }

        if (string.IsNullOrWhiteSpace(command.Reason) ||
            command.Reason.Trim().Length > RoomRetirementProcess.ReasonMaxLength)
        {
            yield return "Reason is required and is too long.";
        }

        if (string.IsNullOrWhiteSpace(command.CanceledBy) ||
            command.CanceledBy.Trim().Length > RoomRetirementProcess.ActorIdMaxLength)
        {
            yield return "CanceledBy is required and is too long.";
        }
    }
}

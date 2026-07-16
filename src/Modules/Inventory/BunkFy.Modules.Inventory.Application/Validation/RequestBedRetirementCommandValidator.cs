namespace BunkFy.Modules.Inventory.Application.Validation;

using BunkFy.Modules.Inventory.Application.Commands;
using BunkFy.Modules.Inventory.Domain.Aggregates;
using Gma.Framework.Cqrs;

internal sealed class RequestBedRetirementCommandValidator : ICommandValidator<RequestBedRetirementCommand>
{
    public IEnumerable<string> Validate(RequestBedRetirementCommand command)
    {
        if (command.PropertyId == Guid.Empty || command.RoomId == Guid.Empty || command.BedId == Guid.Empty)
        {
            yield return "PropertyId, RoomId and BedId are required.";
        }

        if (string.IsNullOrWhiteSpace(command.Reason) || command.Reason.Trim().Length > BedRetirementProcess.ReasonMaxLength)
        {
            yield return "Reason is required and is too long.";
        }

        if (string.IsNullOrWhiteSpace(command.RequestedBy) || command.RequestedBy.Trim().Length > BedRetirementProcess.ActorIdMaxLength)
        {
            yield return "RequestedBy is required and is too long.";
        }
    }
}

namespace BunkFy.Modules.Properties.Application.Validation;

using BunkFy.Modules.Properties.Application.Commands;
using BunkFy.Modules.Properties.Contracts;
using Gma.Framework.Cqrs;

internal sealed class AddBedsCommandValidator
    : ICommandValidator<AddBedsCommand>
{
    public IEnumerable<string> Validate(AddBedsCommand command)
    {
        if (command.OperationId == Guid.Empty)
        {
            yield return "OperationId is required.";
        }

        if (command.PropertyId == Guid.Empty)
        {
            yield return "Property id is required.";
        }

        if (command.RoomId == Guid.Empty)
        {
            yield return "Room id is required.";
        }

        foreach (string error in PropertiesValidation.ValidateExpectedVersion(
                     command.ExpectedRoomVersion,
                     "room"))
        {
            yield return error;
        }

        if (command.Labels is null || command.Labels.Count == 0)
        {
            yield return "At least one bed label is required.";
            yield break;
        }

        if (command.Labels.Count > PropertiesContractLimits.MaximumBedsPerBatch)
        {
            yield return
                $"A bed batch cannot contain more than {PropertiesContractLimits.MaximumBedsPerBatch} beds.";
        }

        foreach (string label in command.Labels)
        {
            foreach (string error in PropertiesValidation.ValidateBedWrite(label))
            {
                yield return error;
            }
        }

        if (command.Labels
            .Select(label => label?.Trim() ?? string.Empty)
            .Distinct(StringComparer.Ordinal)
            .Count() != command.Labels.Count)
        {
            yield return "Bed labels must be unique.";
        }
    }
}

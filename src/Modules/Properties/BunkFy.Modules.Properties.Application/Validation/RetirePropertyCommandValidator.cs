namespace BunkFy.Modules.Properties.Application.Validation;

using BunkFy.Modules.Properties.Application.Commands;
using BunkFy.Modules.Properties.Contracts;
using Gma.Framework.Cqrs;

internal sealed class RetirePropertyCommandValidator : ICommandValidator<RetirePropertyCommand>
{
    public IEnumerable<string> Validate(RetirePropertyCommand command)
    {
        if (command.PropertyId == Guid.Empty)
        {
            yield return "Property id is required.";
        }

        foreach (string error in PropertiesValidation.ValidateExpectedVersion(command.ExpectedVersion, "property"))
        {
            yield return error;
        }

        string? actorId = string.IsNullOrWhiteSpace(command.ActorId) ? null : command.ActorId.Trim();
        if (actorId is not null &&
            (actorId.Length > PropertiesContractLimits.ActorIdMaxLength || actorId.Any(char.IsControl)))
        {
            yield return $"Actor id must be {PropertiesContractLimits.ActorIdMaxLength} characters or fewer and cannot contain control characters.";
        }
    }
}

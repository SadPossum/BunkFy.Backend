namespace Properties.Application.Validation;

using Properties.Application.Commands;
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
    }
}

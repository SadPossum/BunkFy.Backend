namespace BunkFy.Modules.Properties.Application.Validation;

using BunkFy.Modules.Properties.Application.Commands;
using BunkFy.Modules.Properties.Domain.Aggregates;
using Gma.Framework.Cqrs;

internal sealed class CreatePropertyCommandValidator : ICommandValidator<CreatePropertyCommand>
{
    public IEnumerable<string> Validate(CreatePropertyCommand command)
    {
        foreach (string error in PropertiesValidation.ValidatePropertyWrite(command.Name, command.Code, command.TimeZoneId))
        {
            yield return error;
        }
    }
}

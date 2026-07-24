namespace BunkFy.Modules.DataRights.Application.Validation;

using BunkFy.Modules.DataRights.Application.Commands;
using Gma.Framework.Cqrs;

internal sealed class StartDataRightsAnonymisationExecutionCommandValidator
    : ICommandValidator<StartDataRightsAnonymisationExecutionCommand>
{
    public IEnumerable<string> Validate(
        StartDataRightsAnonymisationExecutionCommand command)
    {
        foreach (string error in DataRightsCaseValidation.Mutation(
            command.PropertyId,
            command.CaseId,
            command.ExpectedVersion,
            command.ActorId))
        {
            yield return error;
        }

        if (command.IdempotencyKey == Guid.Empty)
        {
            yield return "IdempotencyKey is required.";
        }
    }
}

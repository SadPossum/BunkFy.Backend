namespace BunkFy.Modules.DataRights.Application.Validation;

using BunkFy.Modules.DataRights.Application.Commands;
using Gma.Framework.Cqrs;

internal sealed class RequestDataRightsExportCommandValidator
    : ICommandValidator<RequestDataRightsExportCommand>
{
    public IEnumerable<string> Validate(RequestDataRightsExportCommand command)
    {
        foreach (string error in DataRightsCaseValidation.Mutation(
            command.Scope,
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

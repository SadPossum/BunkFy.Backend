namespace BunkFy.Modules.DataRights.Application.Validation;

using BunkFy.Modules.DataRights.Application.Commands;
using Gma.Framework.Cqrs;

internal sealed class CancelDataRightsCaseCommandValidator
    : ICommandValidator<CancelDataRightsCaseCommand>
{
    public IEnumerable<string> Validate(CancelDataRightsCaseCommand command) =>
        DataRightsCaseValidation.Mutation(
            command.PropertyId,
            command.CaseId,
            command.ExpectedVersion,
            command.ActorId);
}

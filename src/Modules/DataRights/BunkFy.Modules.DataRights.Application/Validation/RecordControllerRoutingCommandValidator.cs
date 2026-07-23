namespace BunkFy.Modules.DataRights.Application.Validation;

using BunkFy.Modules.DataRights.Application.Commands;
using Gma.Framework.Cqrs;

internal sealed class RecordControllerRoutingCommandValidator
    : ICommandValidator<RecordControllerRoutingCommand>
{
    public IEnumerable<string> Validate(RecordControllerRoutingCommand command) =>
        DataRightsCaseValidation.Mutation(
            command.PropertyId,
            command.CaseId,
            command.ExpectedVersion,
            command.ActorId);
}

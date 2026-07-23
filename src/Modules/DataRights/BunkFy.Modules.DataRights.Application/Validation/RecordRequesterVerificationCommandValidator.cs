namespace BunkFy.Modules.DataRights.Application.Validation;

using BunkFy.Modules.DataRights.Application.Commands;
using Gma.Framework.Cqrs;

internal sealed class RecordRequesterVerificationCommandValidator
    : ICommandValidator<RecordRequesterVerificationCommand>
{
    public IEnumerable<string> Validate(RecordRequesterVerificationCommand command) =>
        DataRightsCaseValidation.Mutation(
            command.PropertyId,
            command.CaseId,
            command.ExpectedVersion,
            command.ActorId);
}

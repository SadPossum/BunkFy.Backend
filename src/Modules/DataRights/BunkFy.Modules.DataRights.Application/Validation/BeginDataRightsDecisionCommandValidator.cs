namespace BunkFy.Modules.DataRights.Application.Validation;

using BunkFy.Modules.DataRights.Application.Commands;
using Gma.Framework.Cqrs;

internal sealed class BeginDataRightsDecisionCommandValidator
    : ICommandValidator<BeginDataRightsDecisionCommand>
{
    public IEnumerable<string> Validate(BeginDataRightsDecisionCommand command) =>
        DataRightsCaseValidation.Mutation(
            command.PropertyId,
            command.CaseId,
            command.ExpectedVersion,
            command.ActorId);
}

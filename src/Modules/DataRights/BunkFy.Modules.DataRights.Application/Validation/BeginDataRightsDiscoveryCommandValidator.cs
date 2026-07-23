namespace BunkFy.Modules.DataRights.Application.Validation;

using BunkFy.Modules.DataRights.Application.Commands;
using Gma.Framework.Cqrs;

internal sealed class BeginDataRightsDiscoveryCommandValidator
    : ICommandValidator<BeginDataRightsDiscoveryCommand>
{
    public IEnumerable<string> Validate(BeginDataRightsDiscoveryCommand command) =>
        DataRightsCaseValidation.Mutation(
            command.PropertyId,
            command.CaseId,
            command.ExpectedVersion,
            command.ActorId);
}

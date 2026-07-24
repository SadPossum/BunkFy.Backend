namespace BunkFy.Modules.DataRights.Application.Validation;

using BunkFy.Modules.DataRights.Application.Commands;
using BunkFy.Modules.DataRights.Contracts;
using Gma.Framework.Cqrs;

internal sealed class RecordDataRightsDecisionCommandValidator
    : ICommandValidator<RecordDataRightsDecisionCommand>
{
    public IEnumerable<string> Validate(RecordDataRightsDecisionCommand command)
    {
        foreach (string error in DataRightsCaseValidation.Mutation(
            command.PropertyId,
            command.CaseId,
            command.ExpectedVersion,
            command.ActorId))
        {
            yield return error;
        }

        if (command.Decision is not DataRightsDecisionOutcome.Approved
            and not DataRightsDecisionOutcome.Denied)
        {
            yield return "Decision is invalid.";
        }

        if (command.Reason is < DataRightsDecisionReason.RequestValidated
            or > DataRightsDecisionReason.UnsupportedOperation)
        {
            yield return "Decision reason is invalid.";
        }
    }
}

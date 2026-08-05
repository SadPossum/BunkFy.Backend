namespace BunkFy.Modules.DataRights.Application.Validation;

using BunkFy.Modules.DataRights.Application.Commands;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using Gma.Framework.Cqrs;

internal sealed class DecideTenantTerminationCommandValidator
    : ICommandValidator<DecideTenantTerminationCommand>
{
    public IEnumerable<string> Validate(
        DecideTenantTerminationCommand command)
    {
        if (command.CaseId == Guid.Empty)
        {
            yield return "CaseId is required.";
        }

        bool validDecision = (
            command.Decision == DataRightsDecisionOutcome.Approved &&
                command.Reason ==
                    DataRightsDecisionReason.RequestValidated &&
                command.ApprovalEvidence is not null) || (
            command.Decision == DataRightsDecisionOutcome.Denied &&
                command.Reason is >=
                    DataRightsDecisionReason.IdentityOrAuthorityNotEstablished and <=
                    DataRightsDecisionReason.UnsupportedOperation &&
                command.ApprovalEvidence is null);
        if (!validDecision)
        {
            yield return "Decision, reason, and approval evidence are inconsistent.";
        }

        if (command.ExpectedVersion <= 0)
        {
            yield return "ExpectedVersion must be positive.";
        }

        string actor = command.ActorId?.Trim() ?? string.Empty;
        if (actor.Length is 0 or > DataRightsCase.ActorIdMaxLength)
        {
            yield return "ActorId is required and must be within the supported limit.";
        }
    }
}

namespace BunkFy.Modules.DataRights.Application.Validation;

using BunkFy.Modules.DataRights.Application.Commands;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using Gma.Framework.Cqrs;

internal sealed class RequestTenantTerminationCommandValidator
    : ICommandValidator<RequestTenantTerminationCommand>
{
    public IEnumerable<string> Validate(
        RequestTenantTerminationCommand command)
    {
        if (command.RequestId == Guid.Empty)
        {
            yield return "RequestId is required.";
        }

        if (command.RequesterRelationship is not
                DataRightsRequesterRelationship.ControllerInitiated and not
                DataRightsRequesterRelationship.TenantOwner)
        {
            yield return "RequesterRelationship is invalid.";
        }

        string actor = command.ActorId?.Trim() ?? string.Empty;
        if (actor.Length is 0 or > DataRightsCase.ActorIdMaxLength)
        {
            yield return "ActorId is required and must be within the supported limit.";
        }
    }
}

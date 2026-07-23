namespace BunkFy.Modules.DataRights.Application.Validation;

using BunkFy.Modules.DataRights.Application.Commands;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using Gma.Framework.Cqrs;

internal sealed class CreateDataRightsCaseCommandValidator
    : ICommandValidator<CreateDataRightsCaseCommand>
{
    private const DataRightsOperation KnownOperations =
        DataRightsOperation.AccessExport |
        DataRightsOperation.Correction |
        DataRightsOperation.Restriction |
        DataRightsOperation.Erasure |
        DataRightsOperation.Anonymisation;

    public IEnumerable<string> Validate(CreateDataRightsCaseCommand command)
    {
        if (command.PropertyId == Guid.Empty)
        {
            yield return "PropertyId is required.";
        }

        if (command.RequestedOperations == DataRightsOperation.None ||
            (command.RequestedOperations & ~KnownOperations) != DataRightsOperation.None)
        {
            yield return "RequestedOperations is invalid.";
        }

        if (command.RequesterRelationship == DataRightsRequesterRelationship.Unknown ||
            !Enum.IsDefined(command.RequesterRelationship) ||
            command.RequesterRelationship == DataRightsRequesterRelationship.TenantOwner)
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

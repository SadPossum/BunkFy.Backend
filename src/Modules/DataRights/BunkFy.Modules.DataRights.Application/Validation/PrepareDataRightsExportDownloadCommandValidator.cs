namespace BunkFy.Modules.DataRights.Application.Validation;

using BunkFy.Modules.DataRights.Application.Commands;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using Gma.Framework.Cqrs;

internal sealed class PrepareDataRightsExportDownloadCommandValidator
    : ICommandValidator<PrepareDataRightsExportDownloadCommand>
{
    public IEnumerable<string> Validate(
        PrepareDataRightsExportDownloadCommand command)
    {
        if (command.Scope is null ||
            command.CaseId == Guid.Empty ||
            command.ArtifactId == Guid.Empty)
        {
            yield return "Scope, CaseId, and ArtifactId are required.";
        }

        string actor = command.ActorId?.Trim() ?? string.Empty;
        if (actor.Length is 0 or > DataRightsCase.ActorIdMaxLength)
        {
            yield return
                "ActorId is required and must be within the supported limit.";
        }
    }
}

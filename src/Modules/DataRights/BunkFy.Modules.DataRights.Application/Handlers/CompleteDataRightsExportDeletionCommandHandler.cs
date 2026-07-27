namespace BunkFy.Modules.DataRights.Application.Handlers;

using BunkFy.Modules.DataRights.Application.Commands;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Models;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;

internal sealed class CompleteDataRightsExportDeletionCommandHandler(
    IDataRightsExportArtifactRepository artifacts,
    IDataRightsExportAuditSink audit,
    ISystemClock clock)
    : ICommandHandler<CompleteDataRightsExportDeletionCommand, Unit>
{
    private const string Actor = "system:data-rights-export-retention";

    public async Task<Result<Unit>> HandleAsync(
        CompleteDataRightsExportDeletionCommand command,
        CancellationToken cancellationToken)
    {
        DataRightsExportArtifact? artifact = await artifacts.GetAsync(
            command.Scope,
            command.ArtifactId,
            cancellationToken).ConfigureAwait(false);
        if (artifact is null)
        {
            return Result.Failure<Unit>(
                DataRightsApplicationErrors.ExportArtifactNotFound);
        }

        if (artifact.CaseId != command.CaseId ||
            artifact.DecisionRevision != command.DecisionRevision)
        {
            return Result.Failure<Unit>(
                DataRightsApplicationErrors.ExportGenerationConflict);
        }

        bool alreadyDeleted =
            artifact.State == DataRightsExportArtifactState.Deleted;
        DateTimeOffset nowUtc = clock.UtcNow;
        Result deleted = artifact.MarkDeleted(command.RunId, nowUtc);
        if (deleted.IsFailure)
        {
            return Result.Failure<Unit>(deleted.Error);
        }

        if (!alreadyDeleted)
        {
            await audit.RecordAsync(
                DataRightsExportAuditFacts.Create(
                    artifact,
                    DataRightsExportAuditAction.Deleted,
                    Actor,
                    "deleted",
                    nowUtc),
                cancellationToken).ConfigureAwait(false);
        }

        return Result.Success(Unit.Value);
    }
}

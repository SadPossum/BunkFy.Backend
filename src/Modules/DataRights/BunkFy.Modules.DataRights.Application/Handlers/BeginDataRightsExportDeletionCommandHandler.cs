namespace BunkFy.Modules.DataRights.Application.Handlers;

using BunkFy.Modules.DataRights.Application.Commands;
using BunkFy.Modules.DataRights.Application.Models;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Models;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;

internal sealed class BeginDataRightsExportDeletionCommandHandler(
    IDataRightsExportArtifactRepository artifacts,
    IDataRightsExportAuditSink audit,
    ISystemClock clock)
    : ICommandHandler<
        BeginDataRightsExportDeletionCommand,
        DataRightsExportDeletionStart>
{
    private const string Actor = "system:data-rights-export-retention";

    public async Task<Result<DataRightsExportDeletionStart>> HandleAsync(
        BeginDataRightsExportDeletionCommand command,
        CancellationToken cancellationToken)
    {
        DataRightsExportArtifact? artifact = await artifacts.GetAsync(
            command.Scope,
            command.ArtifactId,
            cancellationToken).ConfigureAwait(false);
        if (artifact is null)
        {
            return Result.Failure<DataRightsExportDeletionStart>(
                DataRightsApplicationErrors.ExportArtifactNotFound);
        }

        if (artifact.CaseId != command.CaseId ||
            artifact.DecisionRevision != command.DecisionRevision)
        {
            return Result.Failure<DataRightsExportDeletionStart>(
                DataRightsApplicationErrors.ExportGenerationConflict);
        }

        if (artifact.State == DataRightsExportArtifactState.Deleted)
        {
            return Result.Success(new DataRightsExportDeletionStart(
                DeletionRequired: false,
                artifact.Id));
        }

        DateTimeOffset nowUtc = clock.UtcNow;
        long versionBeforeExpiry = artifact.Version;
        Result expired = artifact.MarkExpired(nowUtc);
        if (expired.IsFailure)
        {
            return Result.Failure<DataRightsExportDeletionStart>(expired.Error);
        }

        if (artifact.Version != versionBeforeExpiry)
        {
            await audit.RecordAsync(
                DataRightsExportAuditFacts.Create(
                    artifact,
                    DataRightsExportAuditAction.Expired,
                    Actor,
                    "expired",
                    nowUtc),
                cancellationToken).ConfigureAwait(false);
        }

        long versionBeforeDeletion = artifact.Version;
        Result deleting = artifact.BeginDeletion(command.RunId, nowUtc);
        if (deleting.IsFailure)
        {
            return Result.Failure<DataRightsExportDeletionStart>(deleting.Error);
        }

        if (artifact.Version != versionBeforeDeletion)
        {
            await audit.RecordAsync(
                DataRightsExportAuditFacts.Create(
                    artifact,
                    DataRightsExportAuditAction.DeletionStarted,
                    Actor,
                    "started",
                    nowUtc),
                cancellationToken).ConfigureAwait(false);
        }

        return Result.Success(new DataRightsExportDeletionStart(
            DeletionRequired: true,
            artifact.Id));
    }
}

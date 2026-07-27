namespace BunkFy.Modules.DataRights.Application.Handlers;

using BunkFy.Modules.DataRights.Application.Commands;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Models;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;

internal sealed class CompleteDataRightsExportGenerationCommandHandler(
    IDataRightsCaseRepository cases,
    IDataRightsExportArtifactRepository artifacts,
    IDataRightsExportAuditSink audit)
    : ICommandHandler<CompleteDataRightsExportGenerationCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(
        CompleteDataRightsExportGenerationCommand command,
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

        DataRightsCase? dataRightsCase = await cases.GetAsync(
            command.Scope,
            command.CaseId,
            cancellationToken).ConfigureAwait(false);
        if (dataRightsCase is null)
        {
            return Result.Failure<Unit>(
                DataRightsApplicationErrors.CaseNotFound);
        }

        bool alreadyAvailable =
            artifact.State == DataRightsExportArtifactState.Available;
        Result transition = artifact.MarkAvailable(
            command.RunId,
            command.Attempt,
            command.ProtectedArtifact.StorageKey,
            command.ProtectedArtifact.EncryptedByteLength,
            command.ProtectedArtifact.PlaintextSha256,
            command.ProtectedArtifact.EncryptionKeyVersion,
            command.ProtectedArtifact.FormatVersion,
            command.ProtectedArtifact.AvailableAtUtc,
            command.ProtectedArtifact.ExpiresAtUtc);
        if (transition.IsFailure)
        {
            return Result.Failure<Unit>(transition.Error);
        }

        Result completed = dataRightsCase.CompleteAccessExport(
            command.DecisionRevision,
            "system:data-rights-export",
            command.ProtectedArtifact.AvailableAtUtc);
        if (completed.IsFailure)
        {
            return Result.Failure<Unit>(completed.Error);
        }

        if (!alreadyAvailable)
        {
            await audit.RecordAsync(
                DataRightsExportAuditFacts.Create(
                    artifact,
                    DataRightsExportAuditAction.GenerationCompleted,
                    "system:data-rights-export",
                    "available",
                    command.ProtectedArtifact.AvailableAtUtc),
                cancellationToken).ConfigureAwait(false);
        }

        return Result.Success(Unit.Value);
    }
}

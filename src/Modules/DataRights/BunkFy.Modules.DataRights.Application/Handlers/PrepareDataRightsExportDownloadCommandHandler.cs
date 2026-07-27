namespace BunkFy.Modules.DataRights.Application.Handlers;

using BunkFy.Modules.DataRights.Application.Commands;
using BunkFy.Modules.DataRights.Application.Models;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Models;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;

internal sealed class PrepareDataRightsExportDownloadCommandHandler(
    IDataRightsExportArtifactRepository artifacts,
    IDataRightsExportArtifactReader reader,
    IDataRightsExportAuditSink audit,
    IScopeContext scopeContext,
    ISystemClock clock)
    : ICommandHandler<
        PrepareDataRightsExportDownloadCommand,
        DataRightsExportDownload>
{
    public async Task<Result<DataRightsExportDownload>> HandleAsync(
        PrepareDataRightsExportDownloadCommand command,
        CancellationToken cancellationToken)
    {
        if (!scopeContext.IsEnabled ||
            string.IsNullOrWhiteSpace(scopeContext.ScopeId))
        {
            return Result.Failure<DataRightsExportDownload>(
                DataRightsApplicationErrors.TenantRequired);
        }

        DateTimeOffset nowUtc = clock.UtcNow;
        DataRightsExportArtifact? artifact = await artifacts.GetAsync(
            command.Scope,
            command.ArtifactId,
            cancellationToken).ConfigureAwait(false);
        if (artifact is null || artifact.CaseId != command.CaseId)
        {
            await audit.RecordAsync(
                DataRightsExportAuditFacts.Create(
                    scopeContext.ScopeId,
                    command.Scope,
                    command.ArtifactId,
                    command.CaseId,
                    DataRightsExportAuditAction.Download,
                    command.ActorId,
                    DataRightsApplicationErrors.ExportArtifactNotFound.Code,
                    nowUtc),
                cancellationToken).ConfigureAwait(false);
            return Result.Failure<DataRightsExportDownload>(
                DataRightsApplicationErrors.ExportArtifactNotFound);
        }

        if (nowUtc >= artifact.ExpiresAtUtc)
        {
            await this.RecordDeniedAsync(
                artifact,
                command.ActorId,
                DataRightsApplicationErrors.ExportArtifactExpired.Code,
                nowUtc,
                cancellationToken).ConfigureAwait(false);
            return Result.Failure<DataRightsExportDownload>(
                DataRightsApplicationErrors.ExportArtifactExpired);
        }

        if (artifact.State != DataRightsExportArtifactState.Available)
        {
            await this.RecordDeniedAsync(
                artifact,
                command.ActorId,
                DataRightsApplicationErrors.ExportArtifactNotAvailable.Code,
                nowUtc,
                cancellationToken).ConfigureAwait(false);
            return Result.Failure<DataRightsExportDownload>(
                DataRightsApplicationErrors.ExportArtifactNotAvailable);
        }

        DataRightsExportDownload download;
        try
        {
            download = await reader.OpenVerifiedAsync(
                artifact,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            await this.RecordDeniedAsync(
                artifact,
                command.ActorId,
                DataRightsApplicationErrors.ExportArtifactVerificationFailed.Code,
                nowUtc,
                cancellationToken).ConfigureAwait(false);
            return Result.Failure<DataRightsExportDownload>(
                DataRightsApplicationErrors.ExportArtifactVerificationFailed);
        }

        DateTimeOffset verifiedAtUtc = clock.UtcNow;
        if (verifiedAtUtc >= artifact.ExpiresAtUtc)
        {
            await download.Content.DisposeAsync().ConfigureAwait(false);
            await this.RecordDeniedAsync(
                artifact,
                command.ActorId,
                DataRightsApplicationErrors.ExportArtifactExpired.Code,
                verifiedAtUtc,
                cancellationToken).ConfigureAwait(false);
            return Result.Failure<DataRightsExportDownload>(
                DataRightsApplicationErrors.ExportArtifactExpired);
        }

        try
        {
            await audit.RecordAsync(
                DataRightsExportAuditFacts.Create(
                    artifact,
                    DataRightsExportAuditAction.Download,
                    command.ActorId,
                    "succeeded",
                    verifiedAtUtc),
                cancellationToken).ConfigureAwait(false);
            return Result.Success(download);
        }
        catch
        {
            await download.Content.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private Task RecordDeniedAsync(
        DataRightsExportArtifact artifact,
        string actorId,
        string outcomeCode,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken) =>
        audit.RecordAsync(
            DataRightsExportAuditFacts.Create(
                artifact,
                DataRightsExportAuditAction.Download,
                actorId,
                outcomeCode,
                occurredAtUtc),
            cancellationToken);
}

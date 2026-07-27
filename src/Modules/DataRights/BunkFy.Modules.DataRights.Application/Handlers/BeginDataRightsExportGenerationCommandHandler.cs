namespace BunkFy.Modules.DataRights.Application.Handlers;

using BunkFy.Modules.DataRights.Application.Commands;
using BunkFy.Modules.DataRights.Application.Models;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Models;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;

internal sealed class BeginDataRightsExportGenerationCommandHandler(
    IDataRightsCaseRepository cases,
    IDataRightsExportArtifactRepository artifacts,
    IEnumerable<IDataRightsSubjectExportContributor> contributors,
    IDataRightsExportAuditSink audit,
    IScopeContext scopeContext,
    ISystemClock clock)
    : ICommandHandler<
        BeginDataRightsExportGenerationCommand,
        DataRightsExportGenerationStart>
{
    public async Task<Result<DataRightsExportGenerationStart>> HandleAsync(
        BeginDataRightsExportGenerationCommand command,
        CancellationToken cancellationToken)
    {
        if (!scopeContext.IsEnabled || string.IsNullOrWhiteSpace(scopeContext.ScopeId))
        {
            return Result.Failure<DataRightsExportGenerationStart>(
                DataRightsApplicationErrors.TenantRequired);
        }

        DataRightsExportArtifact? artifact = await artifacts.GetAsync(
            command.Scope,
            command.ArtifactId,
            cancellationToken).ConfigureAwait(false);
        if (artifact is null)
        {
            return Result.Failure<DataRightsExportGenerationStart>(
                DataRightsApplicationErrors.ExportArtifactNotFound);
        }

        DataRightsCase? dataRightsCase = await cases.GetAsync(
            command.Scope,
            command.CaseId,
            cancellationToken).ConfigureAwait(false);
        if (dataRightsCase is null)
        {
            return Result.Failure<DataRightsExportGenerationStart>(
                DataRightsApplicationErrors.CaseNotFound);
        }

        if (!DataRightsExportGenerationCase.Matches(
                dataRightsCase,
                artifact,
                command.Scope,
                command.CaseId,
                command.DecisionRevision,
                out DataRightsSubjectCoordinate[] subjects))
        {
            return Result.Failure<DataRightsExportGenerationStart>(
                DataRightsApplicationErrors.ExportGenerationConflict);
        }

        Result<IReadOnlyDictionary<string, IDataRightsSubjectExportContributor>>
            contributorSet = DataRightsExportContributorSet.Resolve(
                contributors,
                command.Scope.CaseType,
                subjects);
        if (contributorSet.IsFailure)
        {
            return Result.Failure<DataRightsExportGenerationStart>(
                contributorSet.Error);
        }

        bool dispatchRequired =
            artifact.State != DataRightsExportArtifactState.Available;
        DateTimeOffset startedAtUtc = clock.UtcNow;
        Result transition = artifact.BeginGeneration(
            command.RunId,
            command.Attempt,
            "system:data-rights-export",
            startedAtUtc);
        if (transition.IsFailure)
        {
            return Result.Failure<DataRightsExportGenerationStart>(
                transition.Error);
        }

        if (dispatchRequired)
        {
            await audit.RecordAsync(
                DataRightsExportAuditFacts.Create(
                    artifact,
                    DataRightsExportAuditAction.GenerationStarted,
                    "system:data-rights-export",
                    "started",
                    startedAtUtc),
                cancellationToken).ConfigureAwait(false);
        }

        return Result.Success(new DataRightsExportGenerationStart(
            dispatchRequired,
            artifact.Id,
            scopeContext.ScopeId,
            dataRightsCase.Id,
            command.Scope.CaseType,
            dataRightsCase.PropertyId,
            artifact.DecisionRevision,
            subjects,
            artifact.GenerationStartedAtUtc!.Value,
            artifact.ExpiresAtUtc));
    }
}

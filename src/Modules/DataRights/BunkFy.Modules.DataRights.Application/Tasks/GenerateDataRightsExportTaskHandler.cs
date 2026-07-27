namespace BunkFy.Modules.DataRights.Application.Tasks;

using BunkFy.Modules.DataRights.Application.Commands;
using BunkFy.Modules.DataRights.Application.Models;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Contracts;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Tasks;
using Gma.Framework.Tasks.Cqrs;

internal sealed class GenerateDataRightsExportTaskHandler(
    ITaskCommandDispatcher commandDispatcher,
    IDataRightsExportArtifactGenerator generator,
    IDataRightsExportArtifactObjectStore objectStore)
    : ITaskHandler<GenerateDataRightsExportPayload>
{
    public async Task HandleAsync(
        GenerateDataRightsExportPayload payload,
        TaskExecutionContext context,
        CancellationToken cancellationToken)
    {
        DataRightsCaseScope scope = Scope(payload);
        Result<DataRightsExportGenerationStart> started =
            await commandDispatcher.DispatchAsync<
                BeginDataRightsExportGenerationCommand,
                DataRightsExportGenerationStart>(
                context,
                new BeginDataRightsExportGenerationCommand(
                    scope,
                    payload.ArtifactId,
                    payload.CaseId,
                    payload.DecisionRevision,
                    context.RunId,
                    context.Attempt),
                cancellationToken).ConfigureAwait(false);
        if (started.IsFailure)
        {
            throw Failure(started.Error);
        }

        if (!started.Value.DispatchRequired)
        {
            return;
        }

        DataRightsProtectedExportArtifact protectedArtifact;
        try
        {
            protectedArtifact = await generator.GenerateAsync(
                new DataRightsExportGenerationRequest(
                    started.Value.ArtifactId,
                    started.Value.TenantId,
                    started.Value.CaseId,
                    started.Value.CaseType,
                    started.Value.PropertyId,
                        started.Value.DecisionRevision,
                        started.Value.SelectedSubjects,
                        started.Value.GeneratedAtUtc,
                        started.Value.ExpiresAtUtc),
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _ = await objectStore.DeleteAsync(
                payload.ArtifactId,
                CancellationToken.None).ConfigureAwait(false);
            throw;
        }
        catch (Exception exception)
        {
            _ = await objectStore.DeleteAsync(
                payload.ArtifactId,
                CancellationToken.None).ConfigureAwait(false);
            string code = exception is DataRightsExportGenerationException generation
                ? generation.Code
                : "generation-failed";
            Result<Unit> failed = await commandDispatcher.DispatchAsync<
                FailDataRightsExportGenerationCommand,
                Unit>(
                context,
                new FailDataRightsExportGenerationCommand(
                    scope,
                    payload.ArtifactId,
                    payload.CaseId,
                    payload.DecisionRevision,
                    context.RunId,
                    context.Attempt,
                    code),
                CancellationToken.None).ConfigureAwait(false);
            if (failed.IsFailure)
            {
                throw Failure(failed.Error);
            }

            throw new InvalidOperationException(
                $"DataRights.ExportGenerationFailed:{code}");
        }

        Result<Unit> completed =
            await commandDispatcher.DispatchAsync<
                CompleteDataRightsExportGenerationCommand,
                Unit>(
                context,
                new CompleteDataRightsExportGenerationCommand(
                    scope,
                    payload.ArtifactId,
                    payload.CaseId,
                    payload.DecisionRevision,
                    context.RunId,
                    context.Attempt,
                    protectedArtifact),
                cancellationToken).ConfigureAwait(false);
        if (completed.IsFailure)
        {
            _ = await objectStore.DeleteAsync(
                payload.ArtifactId,
                CancellationToken.None).ConfigureAwait(false);
            throw Failure(completed.Error);
        }
    }

    private static DataRightsCaseScope Scope(
        GenerateDataRightsExportPayload payload) =>
        payload.CaseType switch
        {
            DataRightsCaseType.GuestRights when payload.PropertyId is Guid propertyId =>
                DataRightsCaseScope.ForProperty(propertyId),
            DataRightsCaseType.StaffRights when payload.PropertyId is null =>
                DataRightsCaseScope.Staff,
            _ => throw new InvalidOperationException(
                "DataRights.ExportTaskScopeInvalid")
        };

    private static InvalidOperationException Failure(Error error) =>
        new($"{error.Code}: {error.Message}");
}

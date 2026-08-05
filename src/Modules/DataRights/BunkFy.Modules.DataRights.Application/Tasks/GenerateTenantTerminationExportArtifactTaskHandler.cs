namespace BunkFy.Modules.DataRights.Application.Tasks;

using BunkFy.Modules.DataRights.Application.Commands;
using BunkFy.Modules.DataRights.Application.Models;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Contracts;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Scoping;
using Gma.Framework.Tasks;
using Gma.Framework.Tasks.Cqrs;

internal sealed class GenerateTenantTerminationExportArtifactTaskHandler(
    ITaskCommandDispatcher commandDispatcher,
    ITenantTerminationExportArtifactGenerator generator,
    IScopeContext scopeContext)
    : ITaskHandler<GenerateTenantTerminationExportArtifactPayload>
{
    public async Task HandleAsync(
        GenerateTenantTerminationExportArtifactPayload payload,
        TaskExecutionContext context,
        CancellationToken cancellationToken)
    {
        ValidateBoundary(payload, context, scopeContext);
        Result<TenantTerminationExportArtifactGenerationStart> started =
            await commandDispatcher.DispatchAsync<
                BeginTenantTerminationExportArtifactGenerationCommand,
                TenantTerminationExportArtifactGenerationStart>(
                    context,
                    new(
                        payload.ProcessId,
                        payload.OperationRevision,
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

        try
        {
            TenantTerminationProtectedExportArtifact protectedArtifact =
                await generator.GenerateAsync(
                    started.Value.Artifact,
                    started.Value.Fragments,
                    cancellationToken).ConfigureAwait(false);
            Result<TenantTerminationExportArtifactGenerationCompleted>
                completed = await commandDispatcher.DispatchAsync<
                    CompleteTenantTerminationExportArtifactGenerationCommand,
                    TenantTerminationExportArtifactGenerationCompleted>(
                        context,
                        new(
                            payload.ProcessId,
                            payload.OperationRevision,
                            started.Value.Artifact.Id,
                            context.RunId,
                            context.Attempt,
                            started.Value.ProcessVersion,
                            started.Value.ArtifactVersion,
                            protectedArtifact),
                        cancellationToken).ConfigureAwait(false);
            if (completed.IsFailure)
            {
                throw Failure(completed.Error);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            Result<Unit> failed = await commandDispatcher.DispatchAsync<
                FailTenantTerminationExportArtifactGenerationCommand,
                Unit>(
                    context,
                    new(
                        payload.ProcessId,
                        payload.OperationRevision,
                        started.Value.Artifact.Id,
                        context.RunId,
                        context.Attempt,
                        started.Value.ArtifactVersion,
                        FailureCode(exception)),
                    CancellationToken.None).ConfigureAwait(false);
            if (failed.IsFailure)
            {
                throw Failure(failed.Error);
            }

            throw;
        }
    }

    private static void ValidateBoundary(
        GenerateTenantTerminationExportArtifactPayload payload,
        TaskExecutionContext context,
        IScopeContext scopeContext)
    {
        Guid expectedRunId = TenantTerminationExecutionIdentity
            .CreateExportArtifactTaskRunId(
                payload.ProcessId,
                payload.OperationRevision);
        if (payload.ProcessId == Guid.Empty ||
            payload.OperationRevision <= 0 ||
            context.RunId != expectedRunId ||
            !scopeContext.IsEnabled ||
            string.IsNullOrWhiteSpace(context.ScopeId) ||
            !string.Equals(
                scopeContext.ScopeId,
                context.ScopeId,
                StringComparison.Ordinal) ||
            !string.Equals(
                context.ModuleName,
                DataRightsModuleMetadata.Name,
                StringComparison.Ordinal) ||
            !string.Equals(
                context.TaskName,
                GenerateTenantTerminationExportArtifactPayload.TaskName,
                StringComparison.Ordinal) ||
            context.PayloadVersion !=
                GenerateTenantTerminationExportArtifactPayload.PayloadVersion ||
            context.CorrelationId != payload.ProcessId)
        {
            throw new InvalidOperationException(
                "DataRights.TenantTerminationExecutionBoundaryInvalid");
        }
    }

    private static string FailureCode(Exception exception) =>
        exception is DataRightsExportGenerationException generation
            ? generation.Code
            : "artifact-generation-failed";

    private static InvalidOperationException Failure(Error error) =>
        new($"{error.Code}: {error.Message}");
}

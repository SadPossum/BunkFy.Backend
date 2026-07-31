namespace BunkFy.Host.Worker;

using System.Text.Json;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Workspaces.Contracts;
using Gma.Framework.Tasks.Infrastructure;
using Gma.Framework.Tenancy;
using Microsoft.Extensions.Logging;

internal sealed class WorkspaceTerminationTaskExecutionContextContributor(
    IWorkspaceTerminationFenceReader fences,
    ILogger<WorkspaceTerminationTaskExecutionContextContributor> logger)
    : ITaskExecutionContextContributor
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public async ValueTask<TaskExecutionContextPreparationResult> PrepareAsync(
        TaskExecutionContextPreparationContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!context.Registration.IsTenantScoped())
        {
            return TaskExecutionContextPreparationResult.Success();
        }

        WorkspaceTerminationFenceSnapshot? fence;
        try
        {
            fence = await fences.GetCurrentAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception)
            when (exception is not OperationCanceledException)
        {
            logger.LogError(
                "Workspace termination task admission state is unavailable because {ExceptionType} was raised.",
                exception.GetType().Name);
            return TaskExecutionContextPreparationResult.Failure(
                "Workspace termination task admission state is unavailable.");
        }

        if (fence is null)
        {
            return TaskExecutionContextPreparationResult.Success();
        }

        if (!IsValid(fence))
        {
            logger.LogError(
                "Workspace termination task admission state is invalid.");
            return TaskExecutionContextPreparationResult.Failure(
                "Workspace termination task admission state is unavailable.");
        }

        return IsExactTerminationTask(context, fence.ProcessId)
            ? TaskExecutionContextPreparationResult.Success()
            : TaskExecutionContextPreparationResult.Failure(
                "The workspace is not accepting ordinary task execution.");
    }

    private static bool IsValid(
        WorkspaceTerminationFenceSnapshot fence) =>
        fence.ProcessId != Guid.Empty &&
        fence.TerminationEpoch != Guid.Empty &&
        fence.Version > 0 &&
        fence.State is
            WorkspaceTerminationFenceState.Frozen or
            WorkspaceTerminationFenceState.DestructionStarted or
            WorkspaceTerminationFenceState.Closed;

    public ValueTask CleanupAsync(
        TaskExecutionContextPreparationContext context,
        CancellationToken cancellationToken) =>
        ValueTask.CompletedTask;

    private static bool IsExactTerminationTask(
        TaskExecutionContextPreparationContext context,
        Guid processId)
    {
        if (!string.Equals(
                context.Lease.ModuleName,
                DataRightsModuleMetadata.Name,
                StringComparison.Ordinal) ||
            !string.Equals(
                context.Lease.TaskName,
                ExecuteTenantTerminationOwnerWorkPayload.TaskName,
                StringComparison.Ordinal) ||
            context.Lease.PayloadVersion !=
                ExecuteTenantTerminationOwnerWorkPayload.PayloadVersion)
        {
            return false;
        }

        try
        {
            ExecuteTenantTerminationOwnerWorkPayload? payload =
                JsonSerializer.Deserialize<
                    ExecuteTenantTerminationOwnerWorkPayload>(
                    context.Lease.PayloadJson,
                    JsonOptions);
            return payload is not null &&
                payload.ProcessId == processId &&
                payload.WorkItemId != Guid.Empty &&
                payload.OperationRevision > 0 &&
                payload.Phase != TenantTerminationContributionPhase.Unknown &&
                !string.IsNullOrWhiteSpace(payload.OwnerKey);
        }
        catch (JsonException)
        {
            return false;
        }
    }
}

namespace BunkFy.Modules.DataRights.Application.Tasks;

using System.Text.Json;
using BunkFy.Modules.DataRights.Application.Models;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using Gma.Framework.Naming;
using Gma.Framework.Runtime.Resilience;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Tasks;

internal interface ITenantTerminationTaskScheduler
{
    Task EnqueueAsync(
        string tenantId,
        IReadOnlyCollection<TenantTerminationPlannedDispatch> dispatches,
        CancellationToken cancellationToken);

    Task EnqueueExportArtifactAsync(
        string tenantId,
        Guid processId,
        long operationRevision,
        CancellationToken cancellationToken);

    Task EnqueueVerificationAsync(
        string tenantId,
        Guid processId,
        long operationRevision,
        CancellationToken cancellationToken);
}

internal sealed class UnavailableTenantTerminationTaskScheduler
    : ITenantTerminationTaskScheduler
{
    internal const string ErrorCode =
        "DataRights.TenantTerminationTaskSchedulingUnavailable";

    public Task EnqueueAsync(
        string tenantId,
        IReadOnlyCollection<TenantTerminationPlannedDispatch> dispatches,
        CancellationToken cancellationToken) => Unavailable(cancellationToken);

    public Task EnqueueExportArtifactAsync(
        string tenantId,
        Guid processId,
        long operationRevision,
        CancellationToken cancellationToken) => Unavailable(cancellationToken);

    public Task EnqueueVerificationAsync(
        string tenantId,
        Guid processId,
        long operationRevision,
        CancellationToken cancellationToken) => Unavailable(cancellationToken);

    private static Task Unavailable(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromException(new InvalidOperationException(ErrorCode));
    }
}

internal sealed class TenantTerminationTaskScheduler(
    ITaskRunStore taskRuns,
    ISystemClock clock) : ITenantTerminationTaskScheduler
{
    internal static readonly TimeSpan ContinuationBaseDelay =
        TimeSpan.FromSeconds(5);
    internal static readonly TimeSpan ContinuationMaximumDelay =
        TimeSpan.FromMinutes(5);

    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    public async Task EnqueueAsync(
        string tenantId,
        IReadOnlyCollection<TenantTerminationPlannedDispatch> dispatches,
        CancellationToken cancellationToken)
    {
        string normalizedTenantId = NormalizeTenantId(tenantId);
        ArgumentNullException.ThrowIfNull(dispatches);
        if (dispatches.Count > TenantTerminationContract.MaximumContributors)
        {
            throw InvalidSchedule();
        }

        TenantTerminationPlannedDispatch[] supplied = dispatches.ToArray();
        ValidateDispatchSet(supplied);
        TenantTerminationPlannedDispatch[] ordered = supplied
            .OrderBy(dispatch => dispatch.OwnerKey, StringComparer.Ordinal)
            .ThenBy(dispatch => dispatch.WorkItemId)
            .ToArray();

        DateTimeOffset createdAtUtc = clock.UtcNow;
        if (createdAtUtc == default)
        {
            throw InvalidSchedule();
        }

        foreach (TenantTerminationPlannedDispatch dispatch in ordered)
        {
            TaskRunRequest request = CreateRequest(
                normalizedTenantId,
                dispatch,
                createdAtUtc);
            TaskRunEnqueueResult result = await taskRuns.EnqueueAsync(
                request,
                cancellationToken).ConfigureAwait(false);
            if (!Matches(request, result?.Run))
            {
                throw new InvalidOperationException(
                    "DataRights.TenantTerminationTaskRunConflict");
            }
        }
    }

    public async Task EnqueueExportArtifactAsync(
        string tenantId,
        Guid processId,
        long operationRevision,
        CancellationToken cancellationToken)
    {
        string normalizedTenantId = NormalizeTenantId(tenantId);
        Guid runId = TenantTerminationExecutionIdentity
            .CreateExportArtifactTaskRunId(processId, operationRevision);
        GenerateTenantTerminationExportArtifactPayload payload = new(
            processId,
            operationRevision);
        DateTimeOffset createdAtUtc = clock.UtcNow;
        if (createdAtUtc == default)
        {
            throw InvalidSchedule();
        }

        TaskRunRequest request = new(
            runId,
            DataRightsModuleMetadata.Name,
            GenerateTenantTerminationExportArtifactPayload.TaskName,
            JsonSerializer.Serialize(payload, SerializerOptions),
            createdAtUtc,
            createdAtUtc,
            DataRightsModuleMetadata.TenantTerminationWorkerGroup,
            normalizedTenantId,
            processId,
            requestedBy: TenantTerminationCoordination.ExecutorActorId,
            maxAttempts: TenantTerminationCoordination.MaximumTaskAttempts,
            payloadVersion:
                GenerateTenantTerminationExportArtifactPayload.PayloadVersion,
            deduplicationKey: TenantTerminationExecutionIdentity
                .CreateTaskDeduplicationKey(runId));
        TaskRunEnqueueResult result = await taskRuns.EnqueueAsync(
            request,
            cancellationToken).ConfigureAwait(false);
        if (!Matches(request, result?.Run))
        {
            throw new InvalidOperationException(
                "DataRights.TenantTerminationTaskRunConflict");
        }
    }

    public async Task EnqueueVerificationAsync(
        string tenantId,
        Guid processId,
        long operationRevision,
        CancellationToken cancellationToken)
    {
        string normalizedTenantId = NormalizeTenantId(tenantId);
        Guid runId = TenantTerminationExecutionIdentity
            .CreateVerificationTaskRunId(processId, operationRevision);
        VerifyTenantTerminationPayload payload = new(
            processId,
            operationRevision);
        DateTimeOffset createdAtUtc = clock.UtcNow;
        if (createdAtUtc == default)
        {
            throw InvalidSchedule();
        }

        TaskRunRequest request = new(
            runId,
            DataRightsModuleMetadata.Name,
            VerifyTenantTerminationPayload.TaskName,
            JsonSerializer.Serialize(payload, SerializerOptions),
            createdAtUtc,
            createdAtUtc,
            DataRightsModuleMetadata.TenantTerminationWorkerGroup,
            normalizedTenantId,
            processId,
            requestedBy: TenantTerminationCoordination.ExecutorActorId,
            maxAttempts: TenantTerminationCoordination.MaximumTaskAttempts,
            payloadVersion: VerifyTenantTerminationPayload.PayloadVersion,
            deduplicationKey: TenantTerminationExecutionIdentity
                .CreateTaskDeduplicationKey(runId));
        TaskRunEnqueueResult result = await taskRuns.EnqueueAsync(
            request,
            cancellationToken).ConfigureAwait(false);
        if (!Matches(request, result?.Run))
        {
            throw new InvalidOperationException(
                "DataRights.TenantTerminationTaskRunConflict");
        }
    }

    private static TaskRunRequest CreateRequest(
        string tenantId,
        TenantTerminationPlannedDispatch dispatch,
        DateTimeOffset createdAtUtc)
    {
        (string taskName, int payloadVersion, string payloadJson, string? scopeId) =
            dispatch.ExecutionBoundary switch
            {
                TenantTerminationExecutionBoundary.TenantScopedTask =>
                    CreateScopedTask(tenantId, dispatch),
                TenantTerminationExecutionBoundary.GlobalControlTask =>
                    CreateGlobalTask(tenantId, dispatch),
                _ => throw InvalidSchedule()
            };
        DateTimeOffset scheduledAtUtc = dispatch.DispatchSequence == 1
            ? createdAtUtc
            : createdAtUtc.Add(BoundedExponentialBackoff.Calculate(
                dispatch.DispatchSequence - 1,
                ContinuationBaseDelay,
                ContinuationMaximumDelay,
                maximumExponent: 6));

        return new TaskRunRequest(
            dispatch.TaskRunId,
            DataRightsModuleMetadata.Name,
            taskName,
            payloadJson,
            createdAtUtc,
            scheduledAtUtc,
            DataRightsModuleMetadata.TenantTerminationWorkerGroup,
            scopeId,
            dispatch.ProcessId,
            requestedBy: TenantTerminationCoordination.ExecutorActorId,
            maxAttempts: TenantTerminationCoordination.MaximumTaskAttempts,
            payloadVersion: payloadVersion,
            deduplicationKey: dispatch.TaskDeduplicationKey);
    }

    private static (
        string TaskName,
        int PayloadVersion,
        string PayloadJson,
        string? ScopeId) CreateScopedTask(
            string tenantId,
            TenantTerminationPlannedDispatch dispatch)
    {
        if (dispatch.Phase == TenantTerminationContributionPhase.Export)
        {
            ExecuteTenantTerminationExportOwnerWorkPayload exportPayload = new(
                dispatch.ProcessId,
                dispatch.WorkItemId,
                dispatch.OperationRevision,
                dispatch.OwnerKey);
            return (
                ExecuteTenantTerminationExportOwnerWorkPayload.TaskName,
                ExecuteTenantTerminationExportOwnerWorkPayload.PayloadVersion,
                JsonSerializer.Serialize(exportPayload, SerializerOptions),
                tenantId);
        }

        ExecuteTenantTerminationOwnerWorkPayload payload = new(
            dispatch.ProcessId,
            dispatch.WorkItemId,
            dispatch.OperationRevision,
            dispatch.Phase,
            dispatch.OwnerKey);
        return (
            ExecuteTenantTerminationOwnerWorkPayload.TaskName,
            ExecuteTenantTerminationOwnerWorkPayload.PayloadVersion,
            JsonSerializer.Serialize(payload, SerializerOptions),
            tenantId);
    }

    private static (
        string TaskName,
        int PayloadVersion,
        string PayloadJson,
        string? ScopeId) CreateGlobalTask(
            string tenantId,
            TenantTerminationPlannedDispatch dispatch)
    {
        ExecuteGlobalTenantTerminationOwnerWorkPayload payload = new(
            tenantId,
            dispatch.ProcessId,
            dispatch.WorkItemId,
            dispatch.OperationRevision,
            dispatch.Phase,
            dispatch.OwnerKey);
        return (
            ExecuteGlobalTenantTerminationOwnerWorkPayload.TaskName,
            ExecuteGlobalTenantTerminationOwnerWorkPayload.PayloadVersion,
            JsonSerializer.Serialize(payload, SerializerOptions),
            ScopeId: null);
    }

    private static void ValidateDispatchSet(
        IReadOnlyCollection<TenantTerminationPlannedDispatch> dispatches)
    {
        HashSet<Guid> runIds = [];
        HashSet<string> deduplicationKeys = new(StringComparer.Ordinal);
        Guid? processId = null;
        foreach (TenantTerminationPlannedDispatch? dispatch in dispatches)
        {
            if (dispatch is null ||
                dispatch.ProcessId == Guid.Empty ||
                dispatch.WorkItemId == Guid.Empty ||
                dispatch.OperationRevision <= 0 ||
                dispatch.Phase == TenantTerminationContributionPhase.Unknown ||
                !IsStableOwnerKey(dispatch.OwnerKey) ||
                dispatch.ExecutionBoundary is not (
                    TenantTerminationExecutionBoundary.TenantScopedTask or
                    TenantTerminationExecutionBoundary.GlobalControlTask) ||
                (dispatch.Phase == TenantTerminationContributionPhase.Export &&
                 dispatch.ExecutionBoundary !=
                    TenantTerminationExecutionBoundary.TenantScopedTask) ||
                dispatch.DispatchSequence <= 0 ||
                dispatch.TaskRunId == Guid.Empty ||
                !string.Equals(
                    dispatch.TaskDeduplicationKey,
                    TenantTerminationExecutionIdentity
                        .CreateTaskDeduplicationKey(dispatch.TaskRunId),
                    StringComparison.Ordinal) ||
                !runIds.Add(dispatch.TaskRunId) ||
                !deduplicationKeys.Add(dispatch.TaskDeduplicationKey) ||
                (processId.HasValue && processId != dispatch.ProcessId))
            {
                throw InvalidSchedule();
            }

            processId ??= dispatch.ProcessId;
        }
    }

    private static bool Matches(
        TaskRunRequest request,
        TaskRunDetails? run)
    {
        if (run is null)
        {
            return false;
        }

        TaskRunSummary summary = run.Summary;
        return summary.RunId == request.RunId &&
            string.Equals(
                summary.ModuleName,
                request.ModuleName,
                StringComparison.Ordinal) &&
            string.Equals(
                summary.TaskName,
                request.TaskName,
                StringComparison.Ordinal) &&
            string.Equals(
                summary.WorkerGroup,
                request.WorkerGroup,
                StringComparison.Ordinal) &&
            summary.PayloadVersion == request.PayloadVersion &&
            string.Equals(
                summary.ScopeId,
                request.ScopeId,
                StringComparison.Ordinal) &&
            summary.CorrelationId == request.CorrelationId &&
            summary.MaxAttempts == request.MaxAttempts &&
            string.Equals(
                summary.RequestedBy,
                request.RequestedBy,
                StringComparison.Ordinal) &&
            string.Equals(
                summary.DeduplicationKey,
                request.DeduplicationKey,
                StringComparison.Ordinal) &&
            string.Equals(
                run.PayloadJson,
                request.PayloadJson,
                StringComparison.Ordinal);
    }

    private static string NormalizeTenantId(string tenantId)
    {
        if (!TenantIds.TryNormalize(tenantId, out string? normalized) ||
            !string.Equals(tenantId, normalized, StringComparison.Ordinal))
        {
            throw InvalidSchedule();
        }

        return normalized;
    }

    private static bool IsStableOwnerKey(string? ownerKey)
    {
        string normalized = ownerKey?.Trim() ?? string.Empty;
        return normalized.Length is > 0 and <=
                TenantTerminationOwnerWorkItem.OwnerKeyMaxLength &&
            string.Equals(ownerKey, normalized, StringComparison.Ordinal) &&
            normalized[0] is >= 'a' and <= 'z' &&
            normalized.All(character =>
                character is (>= 'a' and <= 'z') or
                    (>= '0' and <= '9') or '.' or '-' or '_');
    }

    private static InvalidOperationException InvalidSchedule() =>
        new("DataRights.TenantTerminationTaskScheduleInvalid");
}

namespace BunkFy.Modules.Ingestion.AdminApi;

using System.Security.Claims;
using System.Text.Json;
using BunkFy.Adapter.Abstractions;
using Gma.Framework.Administration;
using Gma.Framework.Administration.Api;
using Gma.Framework.Api.Observability;
using Gma.Framework.Api.Results;
using Gma.Framework.Cqrs;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Pagination;
using Gma.Framework.Results;
using Gma.Framework.Tenancy;
using Gma.Framework.Tasks;
using Gma.Modules.TaskRuntime.Application.Commands;
using Gma.Modules.TaskRuntime.Application.Queries;
using BunkFy.Modules.Ingestion.Admin.Contracts;
using BunkFy.Modules.Ingestion.Application;
using BunkFy.Modules.Ingestion.Application.Commands;
using BunkFy.Modules.Ingestion.Application.Queries;
using BunkFy.Modules.Ingestion.Contracts;
using BunkFy.Modules.Ingestion.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public sealed class IngestionAdminApiModule : IAdminApiModule
{
    public string Name => IngestionModuleMetadata.Name;

    public void AddServices(IHostApplicationBuilder builder)
    {
        builder.SelectModuleProfile(IngestionProfiles.Default, "BunkFy.Modules.Ingestion.AdminApi");
        builder.Services.AddIngestionApplication();
        builder.AddIngestionPersistence();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        MapAdapterTypes(endpoints);
        MapParserTypes(endpoints);
        MapConnections(endpoints);
        MapRuns(endpoints);
        MapReceipts(endpoints);
        MapReprocessing(endpoints);
        MapRetention(endpoints);
        MapLegalHolds(endpoints);
        MapProposals(endpoints);
    }

    private static void MapParserTypes(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = Group(endpoints, "parser-types");
        group.MapGet("", async (HttpContext context, AdminApiExecutor executor,
            IRequestDispatcher dispatcher, CancellationToken token) =>
            await executor.ExecuteAsync(context,
                AdminOperation.Create(IngestionAdminOperationNames.ParserTypeList, IngestionAdminPermissions.Read), true,
                ct => dispatcher.QueryAsync(new ListObservationParserCapabilitiesQuery(), ct), token,
                errorStatusCodes: ErrorStatusCodes).ConfigureAwait(false));
    }

    private static void MapAdapterTypes(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = Group(endpoints, "adapter-types");
        group.MapGet("", async (HttpContext context, AdminApiExecutor executor,
            IRequestDispatcher dispatcher, CancellationToken token) =>
            await executor.ExecuteAsync(context,
                AdminOperation.Create(IngestionAdminOperationNames.AdapterTypeList, IngestionAdminPermissions.Read), true,
                ct => dispatcher.QueryAsync(new ListAdapterTypeCapabilitiesQuery(), ct), token,
                errorStatusCodes: ErrorStatusCodes).ConfigureAwait(false));
    }

    private static void MapConnections(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = Group(endpoints, "connections");
        group.MapGet("", async (Guid propertyId, AdapterConnectionStatus? status, int? page, int? pageSize,
            HttpContext context, AdminApiExecutor executor, IRequestDispatcher dispatcher, CancellationToken token) =>
            await executor.ExecuteAsync(context,
                AdminOperation.Create(IngestionAdminOperationNames.ConnectionList, IngestionAdminPermissions.Read), true,
                ct => dispatcher.QueryAsync(new ListAdapterConnectionsQuery(propertyId, status,
                    page ?? PageRequest.DefaultPage, pageSize ?? PageRequest.DefaultPageSize), ct), token,
                errorStatusCodes: ErrorStatusCodes).ConfigureAwait(false));
        group.MapGet("/{connectionId:guid}", async (Guid propertyId, Guid connectionId, HttpContext context,
            AdminApiExecutor executor, IRequestDispatcher dispatcher, CancellationToken token) =>
            await executor.ExecuteAsync(context,
                AdminOperation.Create(IngestionAdminOperationNames.ConnectionGet, IngestionAdminPermissions.Read), true,
                ct => dispatcher.QueryAsync(new GetAdapterConnectionQuery(propertyId, connectionId), ct), token,
                errorStatusCodes: ErrorStatusCodes).ConfigureAwait(false));
        group.MapGet("/{connectionId:guid}/health", async (Guid propertyId, Guid connectionId, HttpContext context,
            AdminApiExecutor executor, IRequestDispatcher dispatcher, CancellationToken token) =>
            await executor.ExecuteAsync(context,
                AdminOperation.Create(IngestionAdminOperationNames.ConnectionHealth, IngestionAdminPermissions.Read), true,
                ct => dispatcher.QueryAsync(new GetAdapterConnectionHealthQuery(propertyId, connectionId), ct), token,
                errorStatusCodes: ErrorStatusCodes).ConfigureAwait(false));
        group.MapPost("", async (Guid propertyId, CreateConnectionRequest request, HttpContext context,
            AdminApiExecutor executor, IRequestDispatcher dispatcher, CancellationToken token) =>
            await executor.ExecuteAsync(context,
                AdminOperation.Create(IngestionAdminOperationNames.ConnectionCreate, IngestionAdminPermissions.ConnectionsManage), true,
                ct => dispatcher.SendAsync(new CreateAdapterConnectionCommand(propertyId, request.AdapterType,
                    request.ExecutionMode, request.ConflictPolicy, request.ConfigurationReference, request.SecretReference), ct),
                token, errorStatusCodes: ErrorStatusCodes).ConfigureAwait(false));
        group.MapPut("/{connectionId:guid}", async (Guid propertyId, Guid connectionId, UpdateConnectionRequest request,
            HttpContext context, AdminApiExecutor executor, IRequestDispatcher dispatcher, CancellationToken token) =>
            await executor.ExecuteAsync(context,
                AdminOperation.Create(IngestionAdminOperationNames.ConnectionUpdate, IngestionAdminPermissions.ConnectionsManage), true,
                ct => dispatcher.SendAsync(new UpdateAdapterConnectionCommand(propertyId, connectionId,
                    request.ExecutionMode, request.ConflictPolicy, request.ConfigurationReference,
                    ResolveSecretReferenceUpdateMode(request.SecretReference, request.ClearSecretReference),
                    request.SecretReference,
                    request.ExpectedVersion), ct), token, errorStatusCodes: ErrorStatusCodes).ConfigureAwait(false));
        group.MapPut("/{connectionId:guid}/polling-schedule", async (
            Guid propertyId, Guid connectionId, ConfigurePollingScheduleRequest request,
            HttpContext context, AdminApiExecutor executor, IRequestDispatcher dispatcher, CancellationToken token) =>
            await executor.ExecuteAsync(context,
                AdminOperation.Create(
                    IngestionAdminOperationNames.ConnectionPollingScheduleConfigure,
                    IngestionAdminPermissions.ConnectionsManage),
                true,
                ct => dispatcher.SendAsync(new ConfigureAdapterConnectionPollingScheduleCommand(
                    propertyId, connectionId, request.IntervalSeconds, request.MaxAttempts, request.ExpectedVersion), ct),
                token,
                errorStatusCodes: ErrorStatusCodes).ConfigureAwait(false));
        group.MapPost("/{connectionId:guid}/polling-schedule/clear", async (
            Guid propertyId, Guid connectionId, VersionRequest request,
            HttpContext context, AdminApiExecutor executor, IRequestDispatcher dispatcher, CancellationToken token) =>
            await executor.ExecuteAsync(context,
                AdminOperation.Create(
                    IngestionAdminOperationNames.ConnectionPollingScheduleClear,
                    IngestionAdminPermissions.ConnectionsManage),
                true,
                ct => dispatcher.SendAsync(new ClearAdapterConnectionPollingScheduleCommand(
                    propertyId, connectionId, request.ExpectedVersion), ct),
                token,
                errorStatusCodes: ErrorStatusCodes).ConfigureAwait(false));
        group.MapPost("/{connectionId:guid}/enable", (Guid propertyId, Guid connectionId, VersionRequest request,
            HttpContext context, AdminApiExecutor executor, IRequestDispatcher dispatcher, CancellationToken token) =>
            SetEnabledAsync(propertyId, connectionId, request, true, context, executor, dispatcher, token));
        group.MapPost("/{connectionId:guid}/disable", (Guid propertyId, Guid connectionId, VersionRequest request,
            HttpContext context, AdminApiExecutor executor, IRequestDispatcher dispatcher, CancellationToken token) =>
            SetEnabledAsync(propertyId, connectionId, request, false, context, executor, dispatcher, token));
        group.MapPost("/{connectionId:guid}/reset-checkpoint", async (Guid propertyId, Guid connectionId,
            ConfirmedVersionRequest request, HttpContext context, AdminApiExecutor executor, IRequestDispatcher dispatcher,
            CancellationToken token) => await executor.ExecuteAsync(context,
                AdminOperation.Create(IngestionAdminOperationNames.ConnectionResetCheckpoint, IngestionAdminPermissions.ConnectionsManage), true,
                async ct => request.Confirmed
                    ? await dispatcher.SendAsync(new ResetAdapterConnectionCheckpointCommand(
                        propertyId, connectionId, request.ExpectedVersion), ct).ConfigureAwait(false)
                    : Result.Failure<AdapterConnectionDto>(AdminErrors.ConfirmationRequired),
                token, errorStatusCodes: ErrorStatusCodes).ConfigureAwait(false));

        group.MapGet("/{connectionId:guid}/credentials", async (
            Guid propertyId, Guid connectionId, int? page, int? pageSize,
            HttpContext context, AdminApiExecutor executor, IRequestDispatcher dispatcher, CancellationToken token) =>
            await executor.ExecuteAsync(context,
                AdminOperation.Create(
                    IngestionAdminOperationNames.CredentialList,
                    IngestionAdminPermissions.CredentialsManage),
                true,
                ct => dispatcher.QueryAsync(new ListAdapterIngressCredentialsQuery(
                    propertyId, connectionId,
                    page ?? PageRequest.DefaultPage,
                    pageSize ?? PageRequest.DefaultPageSize), ct),
                token,
                errorStatusCodes: ErrorStatusCodes).ConfigureAwait(false));
        group.MapPost("/{connectionId:guid}/credentials", async (
            Guid propertyId, Guid connectionId, CreateIngressCredentialRequest request,
            HttpContext context, AdminApiExecutor executor, IRequestDispatcher dispatcher, CancellationToken token) =>
            await executor.ExecuteAsync(context,
                AdminOperation.Create(
                    IngestionAdminOperationNames.CredentialCreate,
                    IngestionAdminPermissions.CredentialsManage),
                true,
                async ct =>
                {
                    Result<CreateAdapterIngressCredentialResponse> result = await dispatcher.SendAsync(
                        new CreateAdapterIngressCredentialCommand(
                            propertyId, connectionId, request.Label, request.ExpiresAtUtc, Actor(context)), ct)
                        .ConfigureAwait(false);
                    if (result.IsSuccess)
                    {
                        context.Response.Headers.CacheControl = "no-store";
                    }

                    return result;
                },
                token,
                errorStatusCodes: ErrorStatusCodes).ConfigureAwait(false));
        group.MapPost("/{connectionId:guid}/credentials/{credentialId:guid}/revoke", async (
            Guid propertyId, Guid connectionId, Guid credentialId, RevokeIngressCredentialRequest request,
            HttpContext context, AdminApiExecutor executor, IRequestDispatcher dispatcher, CancellationToken token) =>
            await executor.ExecuteAsync(context,
                AdminOperation.Create(
                    IngestionAdminOperationNames.CredentialRevoke,
                    IngestionAdminPermissions.CredentialsManage),
                true,
                ct => request.Confirmed
                    ? dispatcher.SendAsync(new RevokeAdapterIngressCredentialCommand(
                        propertyId, connectionId, credentialId, request.ExpectedVersion, Actor(context)), ct)
                    : Task.FromResult(Result.Failure<AdapterIngressCredentialDto>(AdminErrors.ConfirmationRequired)),
                token,
                errorStatusCodes: ErrorStatusCodes).ConfigureAwait(false));
    }

    private static void MapRuns(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = Group(endpoints, "runs");
        group.MapGet("", async (Guid propertyId, Guid? connectionId, IngestionRunStatus? status, int? page, int? pageSize,
            HttpContext context, AdminApiExecutor executor, IRequestDispatcher dispatcher, CancellationToken token) =>
            await executor.ExecuteAsync(context,
                AdminOperation.Create(IngestionAdminOperationNames.RunList, IngestionAdminPermissions.Read), true,
                ct => dispatcher.QueryAsync(new ListIngestionRunsQuery(propertyId, connectionId, status,
                    page ?? PageRequest.DefaultPage, pageSize ?? PageRequest.DefaultPageSize), ct), token,
                errorStatusCodes: ErrorStatusCodes).ConfigureAwait(false));
        group.MapGet("/{runId:guid}", async (Guid propertyId, Guid runId, HttpContext context,
            AdminApiExecutor executor, IRequestDispatcher dispatcher, CancellationToken token) =>
            await executor.ExecuteAsync(context,
                AdminOperation.Create(IngestionAdminOperationNames.RunGet, IngestionAdminPermissions.Read), true,
                ct => dispatcher.QueryAsync(new GetIngestionRunQuery(propertyId, runId), ct), token,
                errorStatusCodes: ErrorStatusCodes).ConfigureAwait(false));
        group.MapPost("", async (Guid propertyId, EnqueueRunRequest request, HttpContext context,
            AdminApiExecutor executor, IRequestDispatcher dispatcher, ITenantContext tenantContext, CancellationToken token) =>
            await executor.ExecuteAsync(context,
                AdminOperation.Create(IngestionAdminOperationNames.RunEnqueue, IngestionAdminPermissions.RunsManage), true,
                async ct =>
                {
                    Result<AdapterConnectionDto> connection = await dispatcher.QueryAsync(
                        new GetAdapterConnectionQuery(propertyId, request.ConnectionId), ct).ConfigureAwait(false);
                    if (connection.IsFailure)
                    {
                        return Result.Failure<Gma.Framework.Tasks.TaskRunDetails>(connection.Error);
                    }

                    if (connection.Value.Status != AdapterConnectionStatus.Enabled)
                    {
                        return Result.Failure<Gma.Framework.Tasks.TaskRunDetails>(
                            IngestionApplicationErrors.ConnectionNotEnabled);
                    }

                    if (connection.Value.ExecutionMode is
                        AdapterExecutionMode.Push or AdapterExecutionMode.RemotePolling)
                    {
                        return Result.Failure<Gma.Framework.Tasks.TaskRunDetails>(
                            IngestionApplicationErrors.AdapterExecutionModeNotTaskRunnable);
                    }

                    if (request.MaxAttempts <= 0 || string.IsNullOrWhiteSpace(tenantContext.TenantId))
                    {
                        return Result.Failure<Gma.Framework.Tasks.TaskRunDetails>(
                            request.MaxAttempts <= 0
                                ? IngestionApplicationErrors.OperatorValueInvalid
                                : IngestionApplicationErrors.ScopeRequired);
                    }

                    return await dispatcher.SendAsync(new EnqueueTaskRunCommand(
                        RunId: null,
                        IngestionModuleMetadata.Name,
                        RunAdapterTaskPayload.TaskName,
                        JsonSerializer.Serialize(new RunAdapterTaskPayload(request.ConnectionId)),
                        request.ScheduledAtUtc,
                        IngestionModuleMetadata.AdapterWorkerGroup,
                        tenantContext.TenantId,
                        request.ConnectionId,
                        Actor(context),
                        request.MaxAttempts,
                        RunAdapterTaskPayload.PayloadVersion,
                        request.DeduplicationKey), ct).ConfigureAwait(false);
                }, token, errorStatusCodes: ErrorStatusCodes).ConfigureAwait(false));
        group.MapPost("/{runId:guid}/retry", (Guid propertyId, Guid runId, RunControlRequest request,
            HttpContext context, AdminApiExecutor executor, IRequestDispatcher dispatcher, CancellationToken token) =>
            ControlRunAsync(propertyId, runId, request, retry: true, context, executor, dispatcher, token));
        group.MapPost("/{runId:guid}/cancel", (Guid propertyId, Guid runId, RunControlRequest request,
            HttpContext context, AdminApiExecutor executor, IRequestDispatcher dispatcher, CancellationToken token) =>
            ControlRunAsync(propertyId, runId, request, retry: false, context, executor, dispatcher, token));
    }

    private static void MapReceipts(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = Group(endpoints, "receipts");
        group.MapGet("", async (Guid propertyId, Guid? connectionId, Guid? runId, ObservationReceiptStatus? status,
            int? page, int? pageSize, HttpContext context, AdminApiExecutor executor, IRequestDispatcher dispatcher,
            CancellationToken token) => await executor.ExecuteAsync(context,
                AdminOperation.Create(IngestionAdminOperationNames.ReceiptList, IngestionAdminPermissions.Read), true,
                ct => dispatcher.QueryAsync(new ListObservationReceiptsQuery(propertyId, connectionId, runId, status,
                    page ?? PageRequest.DefaultPage, pageSize ?? PageRequest.DefaultPageSize), ct), token,
                errorStatusCodes: ErrorStatusCodes).ConfigureAwait(false));
        group.MapGet("/{receiptId:guid}", async (Guid propertyId, Guid receiptId, HttpContext context,
            AdminApiExecutor executor, IRequestDispatcher dispatcher, CancellationToken token) =>
            await executor.ExecuteAsync(context,
                AdminOperation.Create(IngestionAdminOperationNames.ReceiptGet, IngestionAdminPermissions.Read), true,
                ct => dispatcher.QueryAsync(new GetObservationReceiptQuery(propertyId, receiptId), ct), token,
                errorStatusCodes: ErrorStatusCodes).ConfigureAwait(false));
        group.MapGet("/{receiptId:guid}/raw-payload", async (Guid propertyId, Guid receiptId, HttpContext context,
            AdminApiExecutor executor, IRequestDispatcher dispatcher, CancellationToken token) =>
            await executor.ExecuteAsync(context,
                AdminOperation.Create(IngestionAdminOperationNames.ReceiptRawPayloadDownload,
                    IngestionAdminPermissions.RawPayloadsRead), true,
                ct => dispatcher.QueryAsync(new GetObservationRawPayloadQuery(propertyId, receiptId), ct), token,
                onSuccess: payload => RawPayloadDownload(context, receiptId, payload),
                errorStatusCodes: ErrorStatusCodes).ConfigureAwait(false));
    }

    private static void MapRetention(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/admin/ingestion/retention")
            .WithModuleName(IngestionModuleMetadata.Name)
            .WithTags("Ingestion Admin")
            .RequireAuthorization();
        group.MapPost("/raw-payloads/purge", async (
            PurgeRawPayloadsRequest request,
            HttpContext context,
            AdminApiExecutor executor,
            IRequestDispatcher dispatcher,
            ITenantContext tenantContext,
            CancellationToken token) => await executor.ExecuteAsync(
                context,
                AdminOperation.Create(
                    IngestionAdminOperationNames.RetentionRawPayloadPurge,
                    IngestionAdminPermissions.RetentionManage),
                true,
                async ct =>
                {
                    if (!request.Confirmed)
                    {
                        return Result.Failure<Gma.Framework.Tasks.TaskRunDetails>(AdminErrors.ConfirmationRequired);
                    }

                    if (request.BatchSize is <= 0 or > PurgeExpiredRawPayloadsPayload.MaximumBatchSize ||
                        request.MaxBatches is <= 0 or > PurgeExpiredRawPayloadsPayload.MaximumBatches ||
                        request.StaleClaimMinutes is < PurgeExpiredRawPayloadsPayload.MinimumStaleClaimMinutes or
                            > PurgeExpiredRawPayloadsPayload.MaximumStaleClaimMinutes ||
                        request.MaxAttempts <= 0 ||
                        string.IsNullOrWhiteSpace(tenantContext.TenantId))
                    {
                        return Result.Failure<Gma.Framework.Tasks.TaskRunDetails>(
                            IngestionApplicationErrors.RetentionTaskOptionsInvalid);
                    }

                    PurgeExpiredRawPayloadsPayload payload = new(
                        request.BatchSize,
                        request.MaxBatches,
                        request.StaleClaimMinutes);
                    return await dispatcher.SendAsync(new EnqueueTaskRunCommand(
                        RunId: null,
                        IngestionModuleMetadata.Name,
                        PurgeExpiredRawPayloadsPayload.TaskName,
                        JsonSerializer.Serialize(payload),
                        ScheduledAtUtc: null,
                        IngestionModuleMetadata.MaintenanceWorkerGroup,
                        tenantContext.TenantId,
                        CorrelationId: null,
                        Actor(context),
                        request.MaxAttempts,
                        PurgeExpiredRawPayloadsPayload.PayloadVersion,
                        request.DeduplicationKey), ct).ConfigureAwait(false);
                },
                token,
                errorStatusCodes: ErrorStatusCodes).ConfigureAwait(false));
        group.MapPost("/reservation-history/redact", async (
            RedactSensitiveHistoryRequest request,
            HttpContext context,
            AdminApiExecutor executor,
            IRequestDispatcher dispatcher,
            ITenantContext tenantContext,
            CancellationToken token) => await executor.ExecuteAsync(
                context,
                AdminOperation.Create(
                    IngestionAdminOperationNames.RetentionSensitiveHistoryRedact,
                    IngestionAdminPermissions.RetentionManage),
                true,
                async ct =>
                {
                    if (!request.Confirmed)
                    {
                        return Result.Failure<Gma.Framework.Tasks.TaskRunDetails>(AdminErrors.ConfirmationRequired);
                    }

                    if (request.BatchSize is <= 0 or > RedactExpiredReservationHistoryPayload.MaximumBatchSize ||
                        request.MaxBatches is <= 0 or > RedactExpiredReservationHistoryPayload.MaximumBatches ||
                        request.MaxAttempts <= 0 ||
                        string.IsNullOrWhiteSpace(tenantContext.TenantId))
                    {
                        return Result.Failure<Gma.Framework.Tasks.TaskRunDetails>(
                            IngestionApplicationErrors.RetentionTaskOptionsInvalid);
                    }

                    RedactExpiredReservationHistoryPayload payload = new(
                        request.BatchSize,
                        request.MaxBatches);
                    return await dispatcher.SendAsync(new EnqueueTaskRunCommand(
                        RunId: null,
                        IngestionModuleMetadata.Name,
                        RedactExpiredReservationHistoryPayload.TaskName,
                        JsonSerializer.Serialize(payload),
                        ScheduledAtUtc: null,
                        IngestionModuleMetadata.MaintenanceWorkerGroup,
                        tenantContext.TenantId,
                        CorrelationId: null,
                        Actor(context),
                        request.MaxAttempts,
                        RedactExpiredReservationHistoryPayload.PayloadVersion,
                        request.DeduplicationKey), ct).ConfigureAwait(false);
                },
                token,
                errorStatusCodes: ErrorStatusCodes).ConfigureAwait(false));
    }

    private static void MapReprocessing(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = Group(endpoints, "reprocessing-attempts");
        group.MapGet("", async (Guid propertyId, Guid? sourceReceiptId, ObservationReprocessingStatus? status,
            int? page, int? pageSize, HttpContext context, AdminApiExecutor executor,
            IRequestDispatcher dispatcher, CancellationToken token) =>
            await executor.ExecuteAsync(context,
                AdminOperation.Create(IngestionAdminOperationNames.ReprocessingList, IngestionAdminPermissions.Read), true,
                ct => dispatcher.QueryAsync(new ListObservationReprocessingAttemptsQuery(
                    propertyId,
                    sourceReceiptId,
                    status,
                    page ?? PageRequest.DefaultPage,
                    pageSize ?? PageRequest.DefaultPageSize), ct), token,
                errorStatusCodes: ErrorStatusCodes).ConfigureAwait(false));
        group.MapGet("/{attemptId:guid}", async (Guid propertyId, Guid attemptId, HttpContext context,
            AdminApiExecutor executor, IRequestDispatcher dispatcher, CancellationToken token) =>
            await executor.ExecuteAsync(context,
                AdminOperation.Create(IngestionAdminOperationNames.ReprocessingGet, IngestionAdminPermissions.Read), true,
                ct => dispatcher.QueryAsync(new GetObservationReprocessingAttemptQuery(propertyId, attemptId), ct), token,
                errorStatusCodes: ErrorStatusCodes).ConfigureAwait(false));
        group.MapPost("", async (Guid propertyId, EnqueueReprocessingRequest request, HttpContext context,
            AdminApiExecutor executor, IRequestDispatcher dispatcher, ITenantContext tenantContext,
            CancellationToken token) => await executor.ExecuteAsync(
                context,
                AdminOperation.Create(
                    IngestionAdminOperationNames.ReprocessingEnqueue,
                    IngestionAdminPermissions.ReprocessingManage),
                true,
                async ct =>
                {
                    if (!request.Confirmed)
                    {
                        return Result.Failure<TaskRunDetails>(AdminErrors.ConfirmationRequired);
                    }

                    if (request.MaxAttempts is <= 0 or > ReprocessObservationPayload.MaximumAttempts ||
                        string.IsNullOrWhiteSpace(tenantContext.TenantId))
                    {
                        return Result.Failure<TaskRunDetails>(IngestionApplicationErrors.OperatorValueInvalid);
                    }

                    string actor = Actor(context);
                    Result<ObservationReprocessingPreparation> prepared = await dispatcher.SendAsync(
                        new PrepareObservationReprocessingCommand(
                            propertyId,
                            request.SourceReceiptId,
                            request.ParserType,
                            request.ParserVersion,
                            actor,
                            request.ScheduledAtUtc),
                        ct).ConfigureAwait(false);
                    if (prepared.IsFailure)
                    {
                        return Result.Failure<TaskRunDetails>(prepared.Error);
                    }

                    try
                    {
                        ReprocessObservationPayload payload = new(
                            prepared.Value.AttemptId,
                            prepared.Value.ParserType,
                            prepared.Value.ParserVersion,
                            request.MaxAttempts);
                        Result<TaskRunDetails> enqueued = await dispatcher.SendAsync(new EnqueueTaskRunCommand(
                            prepared.Value.TaskRunId,
                            IngestionModuleMetadata.Name,
                            ReprocessObservationPayload.TaskName,
                            JsonSerializer.Serialize(payload),
                            prepared.Value.ScheduledAtUtc,
                            IngestionModuleMetadata.MaintenanceWorkerGroup,
                            tenantContext.TenantId,
                            prepared.Value.SourceReceiptId,
                            actor,
                            request.MaxAttempts,
                            ReprocessObservationPayload.PayloadVersion,
                            request.DeduplicationKey ?? $"reprocess:{prepared.Value.AttemptId:N}"), ct)
                            .ConfigureAwait(false);
                        if (enqueued.IsFailure)
                        {
                            _ = await dispatcher.SendAsync(new FailPreparedObservationReprocessingCommand(
                                prepared.Value.AttemptId,
                                IngestionApplicationErrors.ReprocessingEnqueueFailed.Code), CancellationToken.None)
                                .ConfigureAwait(false);
                        }

                        return enqueued;
                    }
                    catch
                    {
                        _ = await dispatcher.SendAsync(new FailPreparedObservationReprocessingCommand(
                            prepared.Value.AttemptId,
                            IngestionApplicationErrors.ReprocessingEnqueueFailed.Code), CancellationToken.None)
                            .ConfigureAwait(false);
                        throw;
                    }
                },
                token,
                errorStatusCodes: ErrorStatusCodes).ConfigureAwait(false));
        group.MapPost("/{attemptId:guid}/cancel", async (Guid propertyId, Guid attemptId,
            ReprocessingControlRequest request, HttpContext context, AdminApiExecutor executor,
            IRequestDispatcher dispatcher, CancellationToken token) => await executor.ExecuteAsync(
                context,
                AdminOperation.Create(
                    IngestionAdminOperationNames.ReprocessingCancel,
                    IngestionAdminPermissions.ReprocessingManage),
                true,
                async ct =>
                {
                    if (!request.Confirmed)
                    {
                        return Result.Failure<Unit>(AdminErrors.ConfirmationRequired);
                    }

                    Result<ObservationReprocessingAttemptDetailsDto> found = await dispatcher.QueryAsync(
                        new GetObservationReprocessingAttemptQuery(propertyId, attemptId), ct).ConfigureAwait(false);
                    if (found.IsFailure)
                    {
                        return Result.Failure<Unit>(found.Error);
                    }

                    if (found.Value.Attempt.Status is ObservationReprocessingStatus.Succeeded or
                        ObservationReprocessingStatus.NoMatch or ObservationReprocessingStatus.Failed or
                        ObservationReprocessingStatus.Canceled or ObservationReprocessingStatus.Expired)
                    {
                        return Result.Success(Unit.Value);
                    }

                    Result<TaskRunDetails> task = await dispatcher.QueryAsync(
                        new GetTaskRunQuery(found.Value.Attempt.TaskRunId), ct).ConfigureAwait(false);
                    if (task.IsFailure)
                    {
                        return Result.Failure<Unit>(task.Error);
                    }

                    if (task.Value.Summary.Status is TaskRunStatus.Succeeded or TaskRunStatus.Failed or
                        TaskRunStatus.Canceled or TaskRunStatus.TimedOut)
                    {
                        return await dispatcher.SendAsync(
                            new CancelObservationReprocessingCommand(attemptId), ct).ConfigureAwait(false);
                    }

                    Result<Unit> canceled = await dispatcher.SendAsync(
                        new CancelTaskRunCommand(task.Value.Summary.RunId, Actor(context)), ct).ConfigureAwait(false);
                    if (canceled.IsFailure)
                    {
                        return canceled;
                    }

                    if (task.Value.Summary.Status is TaskRunStatus.Queued or TaskRunStatus.RetryScheduled)
                    {
                        return await dispatcher.SendAsync(
                            new CancelObservationReprocessingCommand(attemptId), ct).ConfigureAwait(false);
                    }

                    return Result.Success(Unit.Value);
                },
                token,
                errorStatusCodes: ErrorStatusCodes).ConfigureAwait(false));
    }

    private static void MapProposals(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = Group(endpoints, "proposals");
        group.MapGet("", async (Guid propertyId, ChangeProposalStatus? status, int? page, int? pageSize,
            HttpContext context, AdminApiExecutor executor, IRequestDispatcher dispatcher, CancellationToken token) =>
            await executor.ExecuteAsync(context,
                AdminOperation.Create(IngestionAdminOperationNames.ProposalList, IngestionAdminPermissions.Read), true,
                ct => dispatcher.QueryAsync(new ListChangeProposalsQuery(propertyId, status,
                    page ?? PageRequest.DefaultPage, pageSize ?? PageRequest.DefaultPageSize), ct), token,
                errorStatusCodes: ErrorStatusCodes).ConfigureAwait(false));
        group.MapGet("/{proposalId:guid}", async (Guid propertyId, Guid proposalId, HttpContext context,
            AdminApiExecutor executor, IRequestDispatcher dispatcher, CancellationToken token) =>
            await executor.ExecuteAsync(context,
                AdminOperation.Create(IngestionAdminOperationNames.ProposalGet, IngestionAdminPermissions.Read), true,
                ct => dispatcher.QueryAsync(new GetChangeProposalQuery(propertyId, proposalId), ct), token,
                errorStatusCodes: ErrorStatusCodes).ConfigureAwait(false));
        group.MapPost("/{proposalId:guid}/accept", async (Guid propertyId, Guid proposalId, AcceptProposalRequest request,
            HttpContext context, AdminApiExecutor executor, IRequestDispatcher dispatcher, CancellationToken token) =>
            await executor.ExecuteAsync(context,
                AdminOperation.Create(IngestionAdminOperationNames.ProposalAccept, IngestionAdminPermissions.ProposalsDecide), true,
                ct => dispatcher.SendAsync(new AcceptChangeProposalCommand(propertyId, proposalId, Actor(context),
                    request.ExpectedProposalVersion, request.ExpectedReservationDetailsRevision), ct), token,
                errorStatusCodes: ErrorStatusCodes).ConfigureAwait(false));
        group.MapPost("/{proposalId:guid}/reject", async (Guid propertyId, Guid proposalId, RejectProposalRequest request,
            HttpContext context, AdminApiExecutor executor, IRequestDispatcher dispatcher, CancellationToken token) =>
            await executor.ExecuteAsync(context,
                AdminOperation.Create(IngestionAdminOperationNames.ProposalReject, IngestionAdminPermissions.ProposalsDecide), true,
                ct => dispatcher.SendAsync(new RejectChangeProposalCommand(propertyId, proposalId, Actor(context),
                    request.Reason, request.ExpectedProposalVersion), ct), token,
                errorStatusCodes: ErrorStatusCodes).ConfigureAwait(false));
    }

    private static void MapLegalHolds(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = Group(endpoints, "legal-holds");
        group.MapGet("", async (
            Guid propertyId,
            LegalHoldStatus? status,
            int? page,
            int? pageSize,
            HttpContext context,
            AdminApiExecutor executor,
            IRequestDispatcher dispatcher,
            CancellationToken token) => await executor.ExecuteAsync(
                context,
                AdminOperation.Create(
                    IngestionAdminOperationNames.LegalHoldList,
                    IngestionAdminPermissions.LegalHoldsManage),
                true,
                ct => dispatcher.QueryAsync(new ListLegalHoldsQuery(
                    propertyId,
                    status,
                    page ?? PageRequest.DefaultPage,
                    pageSize ?? PageRequest.DefaultPageSize), ct),
                token,
                errorStatusCodes: ErrorStatusCodes).ConfigureAwait(false));
        group.MapGet("/{holdId:guid}", async (
            Guid propertyId,
            Guid holdId,
            HttpContext context,
            AdminApiExecutor executor,
            IRequestDispatcher dispatcher,
            CancellationToken token) => await executor.ExecuteAsync(
                context,
                AdminOperation.Create(
                    IngestionAdminOperationNames.LegalHoldGet,
                    IngestionAdminPermissions.LegalHoldsManage),
                true,
                ct => dispatcher.QueryAsync(new GetLegalHoldQuery(propertyId, holdId), ct),
                token,
                errorStatusCodes: ErrorStatusCodes).ConfigureAwait(false));
        group.MapPost("", async (
            Guid propertyId,
            PlaceLegalHoldRequest request,
            HttpContext context,
            AdminApiExecutor executor,
            IRequestDispatcher dispatcher,
            CancellationToken token) => await executor.ExecuteAsync(
                context,
                AdminOperation.Create(
                    IngestionAdminOperationNames.LegalHoldPlace,
                    IngestionAdminPermissions.LegalHoldsManage),
                true,
                ct => dispatcher.SendAsync(new PlaceLegalHoldCommand(
                    propertyId,
                    request.Reason,
                    Actor(context)), ct),
                token,
                errorStatusCodes: ErrorStatusCodes).ConfigureAwait(false));
        group.MapPost("/{holdId:guid}/release", async (
            Guid propertyId,
            Guid holdId,
            ReleaseLegalHoldRequest request,
            HttpContext context,
            AdminApiExecutor executor,
            IRequestDispatcher dispatcher,
            CancellationToken token) => await executor.ExecuteAsync(
                context,
                AdminOperation.Create(
                    IngestionAdminOperationNames.LegalHoldRelease,
                    IngestionAdminPermissions.LegalHoldsManage),
                true,
                ct => request.Confirmed
                    ? dispatcher.SendAsync(new ReleaseLegalHoldCommand(
                        propertyId,
                        holdId,
                        request.ExpectedVersion,
                        request.ReleaseReason,
                        Actor(context)), ct)
                    : Task.FromResult(Result.Failure<LegalHoldDto>(AdminErrors.ConfirmationRequired)),
                token,
                errorStatusCodes: ErrorStatusCodes).ConfigureAwait(false));
    }

    private static RouteGroupBuilder Group(IEndpointRouteBuilder endpoints, string segment) =>
        endpoints.MapGroup($"/api/admin/ingestion/properties/{{propertyId:guid}}/{segment}")
            .WithModuleName(IngestionModuleMetadata.Name)
            .WithTags("Ingestion Admin")
            .RequireAuthorization();

    private static IResult RawPayloadDownload(
        HttpContext context,
        Guid receiptId,
        ObservationRawPayload payload)
    {
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.XContentTypeOptions = "nosniff";
        context.Response.Headers.Append("Content-Security-Policy", "sandbox");
        context.Response.Headers.Append("Cross-Origin-Resource-Policy", "same-origin");
        return Results.File(
            payload.Content.ToArray(),
            "application/octet-stream",
            $"ingestion-receipt-{receiptId:N}.payload",
            enableRangeProcessing: false);
    }

    private static Task<IResult> SetEnabledAsync(
        Guid propertyId, Guid connectionId, VersionRequest request, bool enabled, HttpContext context,
        AdminApiExecutor executor, IRequestDispatcher dispatcher, CancellationToken token) => executor.ExecuteAsync(
        context,
        AdminOperation.Create(enabled ? IngestionAdminOperationNames.ConnectionEnable : IngestionAdminOperationNames.ConnectionDisable,
            IngestionAdminPermissions.ConnectionsManage),
        true,
        ct => dispatcher.SendAsync(new SetAdapterConnectionEnabledCommand(
            propertyId, connectionId, enabled, request.ExpectedVersion), ct),
        token,
        errorStatusCodes: ErrorStatusCodes);

    private static Task<IResult> ControlRunAsync(
        Guid propertyId, Guid runId, RunControlRequest request, bool retry, HttpContext context,
        AdminApiExecutor executor, IRequestDispatcher dispatcher, CancellationToken token) => executor.ExecuteAsync(
        context,
        AdminOperation.Create(retry ? IngestionAdminOperationNames.RunRetry : IngestionAdminOperationNames.RunCancel,
            IngestionAdminPermissions.RunsManage),
        true,
        async ct =>
        {
            if (!request.Confirmed)
            {
                return Result.Failure<Unit>(AdminErrors.ConfirmationRequired);
            }

            Result<IngestionRunDto> run = await dispatcher.QueryAsync(
                new GetIngestionRunQuery(propertyId, runId), ct).ConfigureAwait(false);
            if (run.IsFailure)
            {
                return Result.Failure<Unit>(run.Error);
            }

            if (!run.Value.TaskRunId.HasValue)
            {
                return Result.Failure<Unit>(IngestionApplicationErrors.RunNotTaskManaged);
            }

            return retry
                ? await dispatcher.SendAsync(new RetryTaskRunCommand(
                    run.Value.TaskRunId.Value, Actor(context), request.ScheduledAtUtc), ct).ConfigureAwait(false)
                : await dispatcher.SendAsync(new CancelTaskRunCommand(
                    run.Value.TaskRunId.Value, Actor(context)), ct).ConfigureAwait(false);
        },
        token,
        errorStatusCodes: ErrorStatusCodes);

    private static string Actor(HttpContext context)
    {
        string identity = context.User.FindFirst("sub")?.Value
            ?? context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? context.User.Identity?.Name
            ?? $"authenticated:{context.User.Identity?.AuthenticationType ?? "unknown"}";
        return $"admin-api:{identity}";
    }

    public sealed record CreateConnectionRequest(string AdapterType, AdapterExecutionMode ExecutionMode,
        AdapterConflictPolicy ConflictPolicy, string ConfigurationReference, string? SecretReference);
    public sealed record UpdateConnectionRequest(AdapterExecutionMode ExecutionMode, AdapterConflictPolicy ConflictPolicy,
        string ConfigurationReference, string? SecretReference, bool ClearSecretReference, long ExpectedVersion);

    private static SecretReferenceUpdateMode ResolveSecretReferenceUpdateMode(
        string? secretReference,
        bool clearSecretReference) => (secretReference, clearSecretReference) switch
        {
            (not null, true) => SecretReferenceUpdateMode.Unknown,
            (not null, false) => SecretReferenceUpdateMode.Replace,
            (null, true) => SecretReferenceUpdateMode.Clear,
            _ => SecretReferenceUpdateMode.Keep
        };
    public sealed record VersionRequest(long ExpectedVersion);
    public sealed record ConfirmedVersionRequest(long ExpectedVersion, bool Confirmed);
    public sealed record ConfigurePollingScheduleRequest(
        int IntervalSeconds,
        int MaxAttempts,
        long ExpectedVersion);
    public sealed record CreateIngressCredentialRequest(string Label, DateTimeOffset? ExpiresAtUtc = null);
    public sealed record RevokeIngressCredentialRequest(long ExpectedVersion, bool Confirmed);
    public sealed record EnqueueRunRequest(Guid ConnectionId, DateTimeOffset? ScheduledAtUtc = null,
        int MaxAttempts = 3, string? DeduplicationKey = null);
    public sealed record EnqueueReprocessingRequest(
        Guid SourceReceiptId,
        string ParserType,
        int? ParserVersion = null,
        DateTimeOffset? ScheduledAtUtc = null,
        int MaxAttempts = 3,
        string? DeduplicationKey = null,
        bool Confirmed = false);
    public sealed record ReprocessingControlRequest(bool Confirmed);
    public sealed record RunControlRequest(bool Confirmed, DateTimeOffset? ScheduledAtUtc = null);
    public sealed record PurgeRawPayloadsRequest(
        bool Confirmed,
        int BatchSize = PurgeExpiredRawPayloadsPayload.DefaultBatchSize,
        int MaxBatches = PurgeExpiredRawPayloadsPayload.DefaultMaxBatches,
        int StaleClaimMinutes = PurgeExpiredRawPayloadsPayload.DefaultStaleClaimMinutes,
        int MaxAttempts = 3,
        string? DeduplicationKey = null);
    public sealed record RedactSensitiveHistoryRequest(
        bool Confirmed,
        int BatchSize = RedactExpiredReservationHistoryPayload.DefaultBatchSize,
        int MaxBatches = RedactExpiredReservationHistoryPayload.DefaultMaxBatches,
        int MaxAttempts = 3,
        string? DeduplicationKey = null);
    public sealed record PlaceLegalHoldRequest(string Reason);
    public sealed record ReleaseLegalHoldRequest(long ExpectedVersion, string ReleaseReason, bool Confirmed);
    public sealed record AcceptProposalRequest(long ExpectedProposalVersion, long ExpectedReservationDetailsRevision);
    public sealed record RejectProposalRequest(long ExpectedProposalVersion, string Reason);

    private static readonly ApiErrorStatusCodeMap ErrorStatusCodes = ApiErrorStatusCodeMap.Create(
        new(IngestionApplicationErrors.AdapterTypeNotRegistered.Code, StatusCodes.Status400BadRequest),
        new(IngestionApplicationErrors.AdapterExecutionModeUnsupported.Code, StatusCodes.Status400BadRequest),
        new(IngestionApplicationErrors.AdapterExecutionModeNotTaskRunnable.Code, StatusCodes.Status400BadRequest),
        new(IngestionApplicationErrors.PollingIntervalBelowAdapterMinimum.Code, StatusCodes.Status400BadRequest),
        new(IngestionApplicationErrors.ConnectionNotFound.Code, StatusCodes.Status404NotFound),
        new(IngestionApplicationErrors.IngressCredentialNotFound.Code, StatusCodes.Status404NotFound),
        new(IngestionApplicationErrors.IngressCredentialLimitReached.Code, StatusCodes.Status409Conflict),
        new(IngestionApplicationErrors.IngressCredentialsRequirePushMode.Code, StatusCodes.Status409Conflict),
        new(IngestionApplicationErrors.PropertyNotActive.Code, StatusCodes.Status409Conflict),
        new(IngestionApplicationErrors.ConnectionStatusInvalid.Code, StatusCodes.Status400BadRequest),
        new(IngestionApplicationErrors.RunNotFound.Code, StatusCodes.Status404NotFound),
        new(IngestionApplicationErrors.RunStatusInvalid.Code, StatusCodes.Status400BadRequest),
        new(IngestionApplicationErrors.RunNotTaskManaged.Code, StatusCodes.Status409Conflict),
        new(IngestionApplicationErrors.ReceiptNotFound.Code, StatusCodes.Status404NotFound),
        new(IngestionApplicationErrors.ReceiptStatusInvalid.Code, StatusCodes.Status400BadRequest),
        new(IngestionApplicationErrors.ReprocessingAttemptNotFound.Code, StatusCodes.Status404NotFound),
        new(IngestionApplicationErrors.ReprocessingAttemptStatusInvalid.Code, StatusCodes.Status409Conflict),
        new(IngestionApplicationErrors.ReprocessingParserNotRegistered.Code, StatusCodes.Status400BadRequest),
        new(IngestionApplicationErrors.ReprocessingParserSourceUnsupported.Code, StatusCodes.Status400BadRequest),
        new(IngestionApplicationErrors.ReprocessingSourceNotRejected.Code, StatusCodes.Status409Conflict),
        new(IngestionApplicationErrors.ReprocessingScheduleInvalid.Code, StatusCodes.Status400BadRequest),
        new(BunkFy.Modules.Ingestion.Domain.Errors.IngestionDomainErrors.ReprocessingReservationActive.Code, StatusCodes.Status409Conflict),
        new(IngestionApplicationErrors.RawPayloadInvalid.Code, StatusCodes.Status422UnprocessableEntity),
        new(IngestionApplicationErrors.RawPayloadPurgeInProgress.Code, StatusCodes.Status409Conflict),
        new(IngestionApplicationErrors.RawPayloadUnavailable.Code, StatusCodes.Status410Gone),
        new(IngestionApplicationErrors.SecretReferenceUpdateInvalid.Code, StatusCodes.Status400BadRequest),
        new(IngestionApplicationErrors.RetentionTaskOptionsInvalid.Code, StatusCodes.Status400BadRequest),
        new(IngestionApplicationErrors.ConnectionNotEnabled.Code, StatusCodes.Status409Conflict),
        new(IngestionApplicationErrors.OperatorValueInvalid.Code, StatusCodes.Status400BadRequest),
        new(IngestionApplicationErrors.ProposalNotFound.Code, StatusCodes.Status404NotFound),
        new(IngestionApplicationErrors.ProposalStatusInvalid.Code, StatusCodes.Status400BadRequest),
        new(IngestionApplicationErrors.ProposalDecisionConflict.Code, StatusCodes.Status409Conflict),
        new(IngestionApplicationErrors.PropertyNotFound.Code, StatusCodes.Status404NotFound),
        new(IngestionApplicationErrors.LegalHoldNotFound.Code, StatusCodes.Status404NotFound),
        new(IngestionApplicationErrors.LegalHoldStatusInvalid.Code, StatusCodes.Status400BadRequest),
        new(IngestionApplicationErrors.LegalHoldPurgeInProgress.Code, StatusCodes.Status409Conflict),
        new(BunkFy.Modules.Ingestion.Domain.Errors.IngestionDomainErrors.VersionConflict.Code, StatusCodes.Status409Conflict),
        new(BunkFy.Modules.Ingestion.Domain.Errors.IngestionDomainErrors.PollingScheduleRequiresPollingMode.Code, StatusCodes.Status409Conflict),
        new(BunkFy.Modules.Ingestion.Domain.Errors.IngestionDomainErrors.PollingIntervalInvalid.Code, StatusCodes.Status400BadRequest),
        new(BunkFy.Modules.Ingestion.Domain.Errors.IngestionDomainErrors.PollingScheduleAttemptsInvalid.Code, StatusCodes.Status400BadRequest),
        new(BunkFy.Modules.Ingestion.Domain.Errors.IngestionDomainErrors.IngressCredentialLabelInvalid.Code, StatusCodes.Status400BadRequest),
        new(BunkFy.Modules.Ingestion.Domain.Errors.IngestionDomainErrors.IngressCredentialExpiryInvalid.Code, StatusCodes.Status400BadRequest),
        new(BunkFy.Modules.Ingestion.Domain.Errors.IngestionDomainErrors.IngressCredentialActorInvalid.Code, StatusCodes.Status400BadRequest),
        new(BunkFy.Modules.Ingestion.Domain.Errors.IngestionDomainErrors.IngressCredentialAlreadyRevoked.Code, StatusCodes.Status409Conflict),
        new(BunkFy.Modules.Ingestion.Domain.Errors.IngestionDomainErrors.ConnectionAlreadyEnabled.Code, StatusCodes.Status409Conflict),
        new(BunkFy.Modules.Ingestion.Domain.Errors.IngestionDomainErrors.ConnectionAlreadyDisabled.Code, StatusCodes.Status409Conflict),
        new(BunkFy.Modules.Ingestion.Domain.Errors.IngestionDomainErrors.ConnectionMustBeDisabled.Code, StatusCodes.Status409Conflict),
        new(BunkFy.Modules.Ingestion.Domain.Errors.IngestionDomainErrors.DecisionReasonInvalid.Code, StatusCodes.Status400BadRequest),
        new(BunkFy.Modules.Ingestion.Domain.Errors.IngestionDomainErrors.LegalHoldReasonInvalid.Code, StatusCodes.Status400BadRequest),
        new(BunkFy.Modules.Ingestion.Domain.Errors.IngestionDomainErrors.LegalHoldReleaseReasonInvalid.Code, StatusCodes.Status400BadRequest),
        new(BunkFy.Modules.Ingestion.Domain.Errors.IngestionDomainErrors.LegalHoldActorInvalid.Code, StatusCodes.Status400BadRequest),
        new(BunkFy.Modules.Ingestion.Domain.Errors.IngestionDomainErrors.LegalHoldAlreadyReleased.Code, StatusCodes.Status409Conflict));
}

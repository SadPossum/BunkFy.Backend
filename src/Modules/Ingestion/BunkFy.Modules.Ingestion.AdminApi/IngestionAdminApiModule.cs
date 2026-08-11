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
using Gma.Modules.TaskRuntime.Contracts;
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
        MapGlobalIngressControl(endpoints);
        MapRuns(endpoints);
        MapReceipts(endpoints);
        MapReprocessing(endpoints);
        MapRetention(endpoints);
        MapLegalHolds(endpoints);
        MapProposals(endpoints);
    }

    private static void MapGlobalIngressControl(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/admin/ingestion/adapter-ingress-control")
            .WithModuleName(IngestionModuleMetadata.Name)
            .WithTags("Ingestion Admin")
            .RequireAuthorization();

        group.MapGet("", async (
            HttpContext context,
            AdminApiExecutor executor,
            IRequestDispatcher dispatcher,
            CancellationToken token) =>
            await executor.ExecuteAsync(
                context,
                AdminOperation.Create(
                    IngestionAdminOperationNames.IngressGlobalControlGet,
                    IngestionAdminPermissions.IngressGlobalControlManage),
                requireTenant: false,
                ct => dispatcher.QueryAsync(new GetAdapterIngressGlobalControlQuery(), ct),
                token,
                errorStatusCodes: ErrorStatusCodes).ConfigureAwait(false));

        group.MapPost("/stop", (
            GlobalIngressControlRequest request,
            HttpContext context,
            AdminApiExecutor executor,
            IRequestDispatcher dispatcher,
            CancellationToken token) =>
            ExecuteGlobalIngressControlAsync(
                request,
                stop: true,
                context,
                executor,
                dispatcher,
                token));

        group.MapPost("/resume", (
            GlobalIngressControlRequest request,
            HttpContext context,
            AdminApiExecutor executor,
            IRequestDispatcher dispatcher,
            CancellationToken token) =>
            ExecuteGlobalIngressControlAsync(
                request,
                stop: false,
                context,
                executor,
                dispatcher,
                token));
    }

    private static Task<IResult> ExecuteGlobalIngressControlAsync(
        GlobalIngressControlRequest request,
        bool stop,
        HttpContext context,
        AdminApiExecutor executor,
        IRequestDispatcher dispatcher,
        CancellationToken token) =>
        executor.ExecuteAsync(
            context,
            AdminOperation.Create(
                stop
                    ? IngestionAdminOperationNames.IngressGlobalControlStop
                    : IngestionAdminOperationNames.IngressGlobalControlResume,
                IngestionAdminPermissions.IngressGlobalControlManage),
            requireTenant: false,
            ct => request.Confirmed
                ? stop
                    ? dispatcher.SendAsync(
                        new StopAdapterIngressGloballyCommand(
                            request.ExpectedVersion,
                            request.ReasonCode,
                            Actor(context)),
                        ct)
                    : dispatcher.SendAsync(
                        new ResumeAdapterIngressGloballyCommand(
                            request.ExpectedVersion,
                            request.ReasonCode,
                            Actor(context)),
                        ct)
                : Task.FromResult(
                    Result.Failure<AdapterIngressGlobalControlDto>(
                        AdminErrors.ConfirmationRequired)),
            token,
            errorStatusCodes: ErrorStatusCodes);

    private static void MapParserTypes(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = Group(endpoints, "parser-types");
        group.MapGet("", async (HttpContext context, AdminApiExecutor executor,
            IRequestDispatcher dispatcher, CancellationToken token) =>
            await executor.ExecuteAsync(context,
                AdminOperation.Create(IngestionAdminOperationNames.ParserTypeList, IngestionAdminPermissions.Read), true,
                ct => dispatcher.QueryAsync(new ListObservationParserCapabilitiesQuery(), ct), token,
                errorStatusCodes: ErrorStatusCodes).ConfigureAwait(false))
            .Produces<ObservationParserCapabilityListResponse>(StatusCodes.Status200OK);
    }

    private static void MapAdapterTypes(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = Group(endpoints, "adapter-types");
        group.MapGet("", async (HttpContext context, AdminApiExecutor executor,
            IRequestDispatcher dispatcher, CancellationToken token) =>
            await executor.ExecuteAsync(context,
                AdminOperation.Create(IngestionAdminOperationNames.AdapterTypeList, IngestionAdminPermissions.Read), true,
                ct => dispatcher.QueryAsync(new ListAdapterTypeCapabilitiesQuery(), ct), token,
                errorStatusCodes: ErrorStatusCodes).ConfigureAwait(false))
            .Produces<AdapterTypeCapabilityListResponse>(StatusCodes.Status200OK);
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
                errorStatusCodes: ErrorStatusCodes).ConfigureAwait(false))
            .Produces<AdapterConnectionListResponse>(StatusCodes.Status200OK);
        group.MapGet("/{connectionId:guid}", async (Guid propertyId, Guid connectionId, HttpContext context,
            AdminApiExecutor executor, IRequestDispatcher dispatcher, CancellationToken token) =>
            await executor.ExecuteAsync(context,
                AdminOperation.Create(IngestionAdminOperationNames.ConnectionGet, IngestionAdminPermissions.Read), true,
                ct => dispatcher.QueryAsync(new GetAdapterConnectionQuery(propertyId, connectionId), ct), token,
                errorStatusCodes: ErrorStatusCodes).ConfigureAwait(false))
            .Produces<AdapterConnectionDto>(StatusCodes.Status200OK);
        group.MapGet("/{connectionId:guid}/health", async (Guid propertyId, Guid connectionId, HttpContext context,
            AdminApiExecutor executor, IRequestDispatcher dispatcher, CancellationToken token) =>
            await executor.ExecuteAsync(context,
                AdminOperation.Create(IngestionAdminOperationNames.ConnectionHealth, IngestionAdminPermissions.Read), true,
                ct => dispatcher.QueryAsync(new GetAdapterConnectionHealthQuery(propertyId, connectionId), ct), token,
                errorStatusCodes: ErrorStatusCodes).ConfigureAwait(false))
            .Produces<AdapterConnectionHealthDto>(StatusCodes.Status200OK);
        group.MapPost("", async (Guid propertyId, CreateConnectionRequest request, HttpContext context,
            AdminApiExecutor executor, IRequestDispatcher dispatcher, CancellationToken token) =>
            await executor.ExecuteAsync(context,
                AdminOperation.Create(IngestionAdminOperationNames.ConnectionCreate, IngestionAdminPermissions.ConnectionsManage), true,
                ct => dispatcher.SendAsync(new CreateAdapterConnectionCommand(request.OperationId, propertyId, request.AdapterType,
                    request.ExecutionMode, request.ConflictPolicy, request.ConfigurationReference, request.SecretReference), ct),
                token, errorStatusCodes: ErrorStatusCodes).ConfigureAwait(false))
            .Produces<AdapterConnectionMutationReceiptDto>(StatusCodes.Status200OK);
        group.MapPut("/{connectionId:guid}", async (Guid propertyId, Guid connectionId, UpdateConnectionRequest request,
            HttpContext context, AdminApiExecutor executor, IRequestDispatcher dispatcher, CancellationToken token) =>
            await executor.ExecuteAsync(context,
                AdminOperation.Create(IngestionAdminOperationNames.ConnectionUpdate, IngestionAdminPermissions.ConnectionsManage), true,
                ct => dispatcher.SendAsync(new UpdateAdapterConnectionCommand(request.OperationId, propertyId, connectionId,
                    request.ExecutionMode, request.ConflictPolicy, request.ConfigurationReference,
                    ResolveSecretReferenceUpdateMode(request.SecretReference, request.ClearSecretReference),
                    request.SecretReference,
                    request.ExpectedVersion), ct), token, errorStatusCodes: ErrorStatusCodes).ConfigureAwait(false))
            .Produces<AdapterConnectionMutationReceiptDto>(StatusCodes.Status200OK);
        group.MapPut("/{connectionId:guid}/polling-schedule", async (
            Guid propertyId, Guid connectionId, ConfigurePollingScheduleRequest request,
            HttpContext context, AdminApiExecutor executor, IRequestDispatcher dispatcher, CancellationToken token) =>
            await executor.ExecuteAsync(context,
                AdminOperation.Create(
                    IngestionAdminOperationNames.ConnectionPollingScheduleConfigure,
                    IngestionAdminPermissions.ConnectionsManage),
                true,
                ct => dispatcher.SendAsync(new ConfigureAdapterConnectionPollingScheduleCommand(
                    request.OperationId,
                    propertyId,
                    connectionId,
                    request.IntervalSeconds,
                    request.MaxAttempts,
                    request.ExpectedVersion), ct),
                token,
                errorStatusCodes: ErrorStatusCodes).ConfigureAwait(false))
            .Produces<AdapterConnectionMutationReceiptDto>(StatusCodes.Status200OK);
        group.MapPost("/{connectionId:guid}/polling-schedule/clear", async (
            Guid propertyId, Guid connectionId, ConnectionControlRequest request,
            HttpContext context, AdminApiExecutor executor, IRequestDispatcher dispatcher, CancellationToken token) =>
            await executor.ExecuteAsync(context,
                AdminOperation.Create(
                    IngestionAdminOperationNames.ConnectionPollingScheduleClear,
                    IngestionAdminPermissions.ConnectionsManage),
                true,
                ct => dispatcher.SendAsync(new ClearAdapterConnectionPollingScheduleCommand(
                    request.OperationId,
                    propertyId,
                    connectionId,
                    request.ExpectedVersion), ct),
                token,
                errorStatusCodes: ErrorStatusCodes).ConfigureAwait(false))
            .Produces<AdapterConnectionMutationReceiptDto>(StatusCodes.Status200OK);
        group.MapPost("/{connectionId:guid}/enable", (Guid propertyId, Guid connectionId, ConnectionControlRequest request,
            HttpContext context, AdminApiExecutor executor, IRequestDispatcher dispatcher, CancellationToken token) =>
            SetEnabledAsync(propertyId, connectionId, request, true, context, executor, dispatcher, token))
            .Produces<AdapterConnectionMutationReceiptDto>(StatusCodes.Status200OK);
        group.MapPost("/{connectionId:guid}/disable", (Guid propertyId, Guid connectionId, ConnectionControlRequest request,
            HttpContext context, AdminApiExecutor executor, IRequestDispatcher dispatcher, CancellationToken token) =>
            SetEnabledAsync(propertyId, connectionId, request, false, context, executor, dispatcher, token))
            .Produces<AdapterConnectionMutationReceiptDto>(StatusCodes.Status200OK);
        group.MapPost("/{connectionId:guid}/reset-checkpoint", async (Guid propertyId, Guid connectionId,
            ConfirmedConnectionControlRequest request, HttpContext context, AdminApiExecutor executor, IRequestDispatcher dispatcher,
            CancellationToken token) => await executor.ExecuteAsync(context,
                AdminOperation.Create(IngestionAdminOperationNames.ConnectionResetCheckpoint, IngestionAdminPermissions.ConnectionsManage), true,
                async ct => request.Confirmed
                    ? await dispatcher.SendAsync(new ResetAdapterConnectionCheckpointCommand(
                        request.OperationId,
                        propertyId,
                        connectionId,
                        request.ExpectedVersion,
                        Confirmed: true), ct).ConfigureAwait(false)
                    : Result.Failure<AdapterConnectionMutationReceiptDto>(AdminErrors.ConfirmationRequired),
                token, errorStatusCodes: ErrorStatusCodes).ConfigureAwait(false))
            .Produces<AdapterConnectionMutationReceiptDto>(StatusCodes.Status200OK);

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
                errorStatusCodes: ErrorStatusCodes).ConfigureAwait(false))
            .Produces<AdapterIngressCredentialListResponse>(StatusCodes.Status200OK);
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
                            request.OperationId,
                            propertyId,
                            connectionId,
                            request.Label,
                            request.ExpiresAtUtc,
                            Actor(context),
                            request.SourceSystem), ct)
                        .ConfigureAwait(false);
                    if (result.IsSuccess)
                    {
                        context.Response.Headers.CacheControl = "no-store";
                    }

                    return result;
                },
                token,
                errorStatusCodes: ErrorStatusCodes).ConfigureAwait(false))
            .Produces<CreateAdapterIngressCredentialResponse>(StatusCodes.Status200OK);
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
                        request.OperationId,
                        propertyId,
                        connectionId,
                        credentialId,
                        request.ExpectedVersion,
                        Actor(context)), ct)
                    : Task.FromResult(Result.Failure<AdapterIngressCredentialMutationReceiptDto>(
                        AdminErrors.ConfirmationRequired)),
                token,
                errorStatusCodes: ErrorStatusCodes).ConfigureAwait(false))
            .Produces<AdapterIngressCredentialMutationReceiptDto>(StatusCodes.Status200OK);
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
                errorStatusCodes: ErrorStatusCodes).ConfigureAwait(false))
            .Produces<IngestionRunListResponse>(StatusCodes.Status200OK);
        group.MapGet("/{runId:guid}", async (Guid propertyId, Guid runId, HttpContext context,
            AdminApiExecutor executor, IRequestDispatcher dispatcher, CancellationToken token) =>
            await executor.ExecuteAsync(context,
                AdminOperation.Create(IngestionAdminOperationNames.RunGet, IngestionAdminPermissions.Read), true,
                ct => dispatcher.QueryAsync(new GetIngestionRunQuery(propertyId, runId), ct), token,
                errorStatusCodes: ErrorStatusCodes).ConfigureAwait(false))
            .Produces<IngestionRunDto>(StatusCodes.Status200OK);
        group.MapPost("", async (Guid propertyId, EnqueueRunRequest request, HttpContext context,
            AdminApiExecutor executor, IRequestDispatcher dispatcher, ITaskRunEnqueuer taskRunEnqueuer,
            ITenantContext tenantContext, CancellationToken token) =>
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

                    return await taskRunEnqueuer.EnqueueAsync(new TaskRunEnqueueRequest(
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
                        request.DeduplicationKey,
                        RunId: null), ct).ConfigureAwait(false);
                }, token, errorStatusCodes: ErrorStatusCodes).ConfigureAwait(false));
        group.MapPost("/{runId:guid}/retry", (Guid propertyId, Guid runId, RunControlRequest request,
            HttpContext context, AdminApiExecutor executor, IRequestDispatcher dispatcher,
            ITaskRunController taskRunController, CancellationToken token) =>
            ControlRunAsync(propertyId, runId, request, retry: true, context, executor, dispatcher,
                taskRunController, token));
        group.MapPost("/{runId:guid}/cancel", (Guid propertyId, Guid runId, RunControlRequest request,
            HttpContext context, AdminApiExecutor executor, IRequestDispatcher dispatcher,
            ITaskRunController taskRunController, CancellationToken token) =>
            ControlRunAsync(propertyId, runId, request, retry: false, context, executor, dispatcher,
                taskRunController, token));
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
                errorStatusCodes: ErrorStatusCodes).ConfigureAwait(false))
            .Produces<ObservationReceiptListResponse>(StatusCodes.Status200OK);
        group.MapGet("/{receiptId:guid}", async (Guid propertyId, Guid receiptId, HttpContext context,
            AdminApiExecutor executor, IRequestDispatcher dispatcher, CancellationToken token) =>
            await executor.ExecuteAsync(context,
                AdminOperation.Create(IngestionAdminOperationNames.ReceiptGet, IngestionAdminPermissions.Read), true,
                ct => dispatcher.QueryAsync(new GetObservationReceiptQuery(propertyId, receiptId), ct), token,
                errorStatusCodes: ErrorStatusCodes).ConfigureAwait(false))
            .Produces<ObservationReceiptDto>(StatusCodes.Status200OK);
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
            ITaskRunEnqueuer taskRunEnqueuer,
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
                    return await taskRunEnqueuer.EnqueueAsync(new TaskRunEnqueueRequest(
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
                        request.DeduplicationKey,
                        RunId: null), ct).ConfigureAwait(false);
                },
                token,
                errorStatusCodes: ErrorStatusCodes).ConfigureAwait(false));
        group.MapPost("/reservation-history/redact", async (
            RedactSensitiveHistoryRequest request,
            HttpContext context,
            AdminApiExecutor executor,
            IRequestDispatcher dispatcher,
            ITaskRunEnqueuer taskRunEnqueuer,
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
                    return await taskRunEnqueuer.EnqueueAsync(new TaskRunEnqueueRequest(
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
                        request.DeduplicationKey,
                        RunId: null), ct).ConfigureAwait(false);
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
                errorStatusCodes: ErrorStatusCodes).ConfigureAwait(false))
            .Produces<ObservationReprocessingAttemptListResponse>(StatusCodes.Status200OK);
        group.MapGet("/{attemptId:guid}", async (Guid propertyId, Guid attemptId, HttpContext context,
            AdminApiExecutor executor, IRequestDispatcher dispatcher, CancellationToken token) =>
            await executor.ExecuteAsync(context,
                AdminOperation.Create(IngestionAdminOperationNames.ReprocessingGet, IngestionAdminPermissions.Read), true,
                ct => dispatcher.QueryAsync(new GetObservationReprocessingAttemptQuery(propertyId, attemptId), ct), token,
                errorStatusCodes: ErrorStatusCodes).ConfigureAwait(false))
            .Produces<ObservationReprocessingAttemptDetailsDto>(StatusCodes.Status200OK);
        group.MapPost("", async (Guid propertyId, EnqueueReprocessingRequest request, HttpContext context,
            AdminApiExecutor executor, IRequestDispatcher dispatcher, ITaskRunEnqueuer taskRunEnqueuer,
            ITenantContext tenantContext,
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
                        Result<TaskRunDetails> enqueued = await taskRunEnqueuer.EnqueueAsync(
                            new TaskRunEnqueueRequest(
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
                            request.DeduplicationKey ?? $"reprocess:{prepared.Value.AttemptId:N}",
                            prepared.Value.TaskRunId), ct)
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
            IRequestDispatcher dispatcher, ITaskRunReader taskRunReader,
            ITaskRunController taskRunController, CancellationToken token) => await executor.ExecuteAsync(
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

                    Result<TaskRunDetails> task = await taskRunReader.GetAsync(
                        found.Value.Attempt.TaskRunId, ct).ConfigureAwait(false);
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

                    Result canceled = await taskRunController.CancelAsync(
                        task.Value.Summary.RunId, Actor(context), ct).ConfigureAwait(false);
                    if (canceled.IsFailure)
                    {
                        return Result.Failure<Unit>(canceled.Error);
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
                errorStatusCodes: ErrorStatusCodes).ConfigureAwait(false))
            .Produces<ChangeProposalListResponse>(StatusCodes.Status200OK);
        group.MapGet("/{proposalId:guid}", async (Guid propertyId, Guid proposalId, HttpContext context,
            AdminApiExecutor executor, IRequestDispatcher dispatcher, CancellationToken token) =>
            await executor.ExecuteAsync(context,
                AdminOperation.Create(
                    IngestionAdminOperationNames.ProposalGet,
                    IngestionAdminPermissions.SensitiveHistoryRead), true,
                ct => dispatcher.QueryAsync(new GetChangeProposalQuery(propertyId, proposalId), ct), token,
                errorStatusCodes: ErrorStatusCodes).ConfigureAwait(false))
            .Produces<ChangeProposalDto>(StatusCodes.Status200OK);
        group.MapPost("/{proposalId:guid}/accept", async (Guid propertyId, Guid proposalId, AcceptProposalRequest request,
            HttpContext context, AdminApiExecutor executor, IRequestDispatcher dispatcher, CancellationToken token) =>
            await executor.ExecuteAsync(context,
                AdminOperation.Create(IngestionAdminOperationNames.ProposalAccept, IngestionAdminPermissions.ProposalsDecide), true,
                ct => dispatcher.SendAsync(new AcceptChangeProposalCommand(propertyId, proposalId, Actor(context),
                    request.ExpectedProposalVersion, request.ExpectedReservationDetailsRevision), ct), token,
                errorStatusCodes: ErrorStatusCodes).ConfigureAwait(false))
            .Produces<ChangeProposalMutationReceiptDto>(StatusCodes.Status200OK);
        group.MapPost("/{proposalId:guid}/reject", async (Guid propertyId, Guid proposalId, RejectProposalRequest request,
            HttpContext context, AdminApiExecutor executor, IRequestDispatcher dispatcher, CancellationToken token) =>
            await executor.ExecuteAsync(context,
                AdminOperation.Create(IngestionAdminOperationNames.ProposalReject, IngestionAdminPermissions.ProposalsDecide), true,
                ct => dispatcher.SendAsync(new RejectChangeProposalCommand(propertyId, proposalId, Actor(context),
                    request.Reason, request.ExpectedProposalVersion), ct), token,
                errorStatusCodes: ErrorStatusCodes).ConfigureAwait(false))
            .Produces<ChangeProposalMutationReceiptDto>(StatusCodes.Status200OK);
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

    private static RouteGroupBuilder Group(IEndpointRouteBuilder endpoints, string segment)
    {
        RouteGroupBuilder group = endpoints.MapGroup(
                $"/api/admin/ingestion/properties/{{propertyId:guid}}/{segment}")
            .WithModuleName(IngestionModuleMetadata.Name)
            .WithTags("Ingestion Admin")
            .RequireAuthorization();
        group.AddEndpointFilter(SensitiveResponseFilter);
        return group;
    }

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

    private static async ValueTask<object?> SensitiveResponseFilter(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        MarkSensitiveResponse(context.HttpContext);
        return await next(context).ConfigureAwait(false);
    }

    private static void MarkSensitiveResponse(HttpContext context)
    {
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.Pragma = "no-cache";
        context.Response.Headers.Expires = "0";
    }

    private static Task<IResult> SetEnabledAsync(
        Guid propertyId, Guid connectionId, ConnectionControlRequest request, bool enabled, HttpContext context,
        AdminApiExecutor executor, IRequestDispatcher dispatcher, CancellationToken token) => executor.ExecuteAsync(
        context,
        AdminOperation.Create(enabled ? IngestionAdminOperationNames.ConnectionEnable : IngestionAdminOperationNames.ConnectionDisable,
            IngestionAdminPermissions.ConnectionsManage),
        true,
        ct => dispatcher.SendAsync(new SetAdapterConnectionEnabledCommand(
            request.OperationId,
            propertyId,
            connectionId,
            enabled,
            request.ExpectedVersion), ct),
        token,
        errorStatusCodes: ErrorStatusCodes);

    private static Task<IResult> ControlRunAsync(
        Guid propertyId, Guid runId, RunControlRequest request, bool retry, HttpContext context,
        AdminApiExecutor executor, IRequestDispatcher dispatcher, ITaskRunController taskRunController,
        CancellationToken token) => executor.ExecuteAsync(
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

            Result result = retry
                ? await taskRunController.RetryAsync(
                    run.Value.TaskRunId.Value, Actor(context), request.ScheduledAtUtc, ct).ConfigureAwait(false)
                : await taskRunController.CancelAsync(
                    run.Value.TaskRunId.Value, Actor(context), ct).ConfigureAwait(false);
            return result.IsFailure
                ? Result.Failure<Unit>(result.Error)
                : Result.Success(Unit.Value);
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

    public sealed record CreateConnectionRequest(Guid OperationId, string AdapterType, AdapterExecutionMode ExecutionMode,
        AdapterConflictPolicy ConflictPolicy, string ConfigurationReference, string? SecretReference);
    public sealed record UpdateConnectionRequest(Guid OperationId, AdapterExecutionMode ExecutionMode, AdapterConflictPolicy ConflictPolicy,
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
    public sealed record ConnectionControlRequest(
        Guid OperationId,
        long ExpectedVersion);
    public sealed record ConfirmedConnectionControlRequest(
        Guid OperationId,
        long ExpectedVersion,
        bool Confirmed);
    public sealed record ConfigurePollingScheduleRequest(
        Guid OperationId,
        int IntervalSeconds,
        int MaxAttempts,
        long ExpectedVersion);
    public sealed record CreateIngressCredentialRequest(
        Guid OperationId,
        string Label,
        DateTimeOffset? ExpiresAtUtc = null,
        string? SourceSystem = null);
    public sealed record RevokeIngressCredentialRequest(
        Guid OperationId,
        long ExpectedVersion,
        bool Confirmed);
    public sealed record GlobalIngressControlRequest(
        long ExpectedVersion,
        string ReasonCode,
        bool Confirmed);
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

    private static readonly ApiErrorStatusCodeMap ErrorStatusCodes = CreateErrorStatusCodes(
        new(IngestionApplicationErrors.AdapterTypeNotRegistered.Code, StatusCodes.Status400BadRequest),
        new(IngestionApplicationErrors.AdapterExecutionModeUnsupported.Code, StatusCodes.Status400BadRequest),
        new(IngestionApplicationErrors.AdapterExecutionModeNotTaskRunnable.Code, StatusCodes.Status400BadRequest),
        new(IngestionApplicationErrors.PollingIntervalBelowAdapterMinimum.Code, StatusCodes.Status400BadRequest),
        new(IngestionApplicationErrors.ConnectionNotFound.Code, StatusCodes.Status404NotFound),
        new(IngestionApplicationErrors.ConnectionManagementOperationInvalid.Code, StatusCodes.Status400BadRequest),
        new(IngestionApplicationErrors.ConnectionManagementOperationConflict.Code, StatusCodes.Status409Conflict),
        new(IngestionApplicationErrors.IngressCredentialNotFound.Code, StatusCodes.Status404NotFound),
        new(IngestionApplicationErrors.IngressCredentialLimitReached.Code, StatusCodes.Status409Conflict),
        new(IngestionApplicationErrors.IngressCredentialsRequirePushMode.Code, StatusCodes.Status409Conflict),
        new(IngestionApplicationErrors.TenantLifecycleRestricted.Code, StatusCodes.Status423Locked),
        new(IngestionApplicationErrors.TenantLifecycleAdmissionUnavailable.Code, StatusCodes.Status503ServiceUnavailable),
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
        new(BunkFy.Modules.Ingestion.Domain.Errors.IngestionDomainErrors.AdapterIngressControlDecisionInvalid.Code, StatusCodes.Status400BadRequest),
        new(BunkFy.Modules.Ingestion.Domain.Errors.IngestionDomainErrors.AdapterIngressGlobalAlreadyStopped.Code, StatusCodes.Status409Conflict),
        new(BunkFy.Modules.Ingestion.Domain.Errors.IngestionDomainErrors.AdapterIngressGlobalAlreadyActive.Code, StatusCodes.Status409Conflict),
        new(BunkFy.Modules.Ingestion.Domain.Errors.IngestionDomainErrors.ConnectionAlreadyEnabled.Code, StatusCodes.Status409Conflict),
        new(BunkFy.Modules.Ingestion.Domain.Errors.IngestionDomainErrors.ConnectionAlreadyDisabled.Code, StatusCodes.Status409Conflict),
        new(BunkFy.Modules.Ingestion.Domain.Errors.IngestionDomainErrors.ConnectionMustBeDisabled.Code, StatusCodes.Status409Conflict),
        new(BunkFy.Modules.Ingestion.Domain.Errors.IngestionDomainErrors.DecisionReasonInvalid.Code, StatusCodes.Status400BadRequest),
        new(BunkFy.Modules.Ingestion.Domain.Errors.IngestionDomainErrors.LegalHoldReasonInvalid.Code, StatusCodes.Status400BadRequest),
        new(BunkFy.Modules.Ingestion.Domain.Errors.IngestionDomainErrors.LegalHoldReleaseReasonInvalid.Code, StatusCodes.Status400BadRequest),
        new(BunkFy.Modules.Ingestion.Domain.Errors.IngestionDomainErrors.LegalHoldActorInvalid.Code, StatusCodes.Status400BadRequest),
        new(BunkFy.Modules.Ingestion.Domain.Errors.IngestionDomainErrors.LegalHoldAlreadyReleased.Code, StatusCodes.Status409Conflict));

    private static ApiErrorStatusCodeMap CreateErrorStatusCodes(params ApiErrorStatusCode[] entries) =>
        ApiErrorStatusCodeMap.Create(entries.Concat(
            IngestionApplicationErrors.CountryPolicyDenials.Select(error =>
                new ApiErrorStatusCode(error.Code, StatusCodes.Status409Conflict))).ToArray());
}

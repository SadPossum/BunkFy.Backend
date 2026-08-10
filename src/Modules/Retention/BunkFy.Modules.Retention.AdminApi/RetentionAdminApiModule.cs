namespace BunkFy.Modules.Retention.AdminApi;

using System.Security.Claims;
using BunkFy.Modules.Retention.Admin.Contracts;
using BunkFy.Modules.Retention.Application;
using BunkFy.Modules.Retention.Application.Errors;
using BunkFy.Modules.Retention.Application.Queries;
using BunkFy.Modules.Retention.Contracts;
using BunkFy.Modules.Retention.Persistence;
using Gma.Framework.Administration;
using Gma.Framework.Administration.Api;
using Gma.Framework.Api.Observability;
using Gma.Framework.Api.Results;
using Gma.Framework.Cqrs;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Pagination;
using Gma.Framework.Results;
using Gma.Framework.Tasks;
using Gma.Framework.Tenancy;
using Gma.Modules.TaskRuntime.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;

public sealed class RetentionAdminApiModule : IAdminApiModule
{
    public string Name => RetentionModuleMetadata.Name;

    public void AddServices(IHostApplicationBuilder builder)
    {
        builder.SelectModuleProfile(
            RetentionProfiles.Default,
            "BunkFy.Modules.Retention.AdminApi");
        builder.Services.AddRetentionApplication();
        builder.AddRetentionPersistence();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints
            .MapGroup("/api/admin/" + RetentionModuleMetadata.Name)
            .WithModuleName(this.Name)
            .WithTags("Retention Admin")
            .RequireAuthorization();
        group.AddEndpointFilter(SensitiveResponseFilter);

        group.MapGet("/schedules", async (
            int? page,
            int? pageSize,
            HttpContext context,
            AdminApiExecutor executor,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            await executor.ExecuteAsync(
                context,
                AdminOperation.Create(
                    RetentionAdminOperationNames.ScheduleList,
                    RetentionAdminPermissions.Read),
                requireTenant: true,
                token => dispatcher.QueryAsync(
                    new ListRetentionScheduleHealthQuery(
                        page ?? PageRequest.DefaultPage,
                        pageSize ?? PageRequest.DefaultPageSize),
                    token),
                cancellationToken,
                errorStatusCodes: AdminErrorStatusCodes).ConfigureAwait(false))
            .Produces<RetentionScheduleHealthListResponse>(StatusCodes.Status200OK);

        group.MapPost("/runs/{runId:guid}/retry", async (
            Guid runId,
            RetryRetentionRunRequest request,
            HttpContext context,
            AdminApiExecutor executor,
            ITaskRunReader taskRunReader,
            ITaskRunController taskRunController,
            ITenantContext tenantContext,
            CancellationToken cancellationToken) =>
            await executor.ExecuteAsync(
                context,
                AdminOperation.Create(
                    RetentionAdminOperationNames.RunRetry,
                    RetentionAdminPermissions.Retry),
                requireTenant: true,
                token => request.Confirmed
                    ? RetryAsync(
                        runId,
                        request.ScheduledAtUtc,
                        Actor(context),
                        tenantContext.TenantId,
                        taskRunReader,
                        taskRunController,
                        token)
                    : Task.FromResult(
                        Result.Failure<RetentionRunRetryReceiptDto>(
                            AdminErrors.ConfirmationRequired)),
                cancellationToken,
                errorStatusCodes: AdminErrorStatusCodes).ConfigureAwait(false))
            .Produces<RetentionRunRetryReceiptDto>(StatusCodes.Status200OK);
    }

    private static async Task<Result<RetentionRunRetryReceiptDto>> RetryAsync(
        Guid runId,
        DateTimeOffset? scheduledAtUtc,
        string actor,
        string? tenantId,
        ITaskRunReader taskRunReader,
        ITaskRunController taskRunController,
        CancellationToken cancellationToken)
    {
        Result<TaskRunDetails> loaded = await taskRunReader.GetAsync(
            runId,
            cancellationToken).ConfigureAwait(false);
        if (loaded.IsFailure)
        {
            return Result.Failure<RetentionRunRetryReceiptDto>(loaded.Error);
        }

        TaskRunSummary run = loaded.Value.Summary;
        if (string.IsNullOrWhiteSpace(tenantId) ||
            !string.Equals(run.ScopeId, tenantId, StringComparison.Ordinal) ||
            !string.Equals(
                run.ModuleName,
                RetentionModuleMetadata.Name,
                StringComparison.Ordinal) ||
            !string.Equals(
                run.TaskName,
                ExecuteRetentionSchedulePayload.TaskName,
                StringComparison.Ordinal))
        {
            return Result.Failure<RetentionRunRetryReceiptDto>(
                RetentionApplicationErrors.TaskRunUnavailable);
        }

        Result retried = await taskRunController.RetryAsync(
            runId,
            actor,
            scheduledAtUtc,
            cancellationToken).ConfigureAwait(false);
        return retried.IsFailure
            ? Result.Failure<RetentionRunRetryReceiptDto>(retried.Error)
            : Result.Success(new RetentionRunRetryReceiptDto(
                runId,
                scheduledAtUtc));
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

    private static readonly ApiErrorStatusCodeMap AdminErrorStatusCodes =
        ApiErrorStatusCodeMap.Create(
            new(
                RetentionApplicationErrors.TaskRunUnavailable.Code,
                StatusCodes.Status404NotFound),
            new(
                TaskRuntimeOperationErrors.RunNotFound.Code,
                StatusCodes.Status404NotFound),
            new(
                TaskRuntimeOperationErrors.RunCannotBeRetried.Code,
                StatusCodes.Status409Conflict),
            new(
                TaskRuntimeOperationErrors.ConcurrentMutation.Code,
                StatusCodes.Status409Conflict),
            new(
                TaskRuntimeOperationErrors.ScopeClosed.Code,
                StatusCodes.Status423Locked));

    private static string Actor(HttpContext context)
    {
        string identity = context.User.FindFirst("sub")?.Value
            ?? context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? context.User.Identity?.Name
            ?? $"authenticated:{context.User.Identity?.AuthenticationType ?? "unknown"}";
        return $"admin-api:{identity}";
    }

    public sealed record RetryRetentionRunRequest(
        bool Confirmed,
        DateTimeOffset? ScheduledAtUtc);
}

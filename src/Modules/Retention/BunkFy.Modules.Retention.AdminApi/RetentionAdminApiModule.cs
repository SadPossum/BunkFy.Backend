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
using Gma.Framework.Cqrs;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Results;
using Gma.Framework.Tasks;
using Gma.Framework.Tenancy;
using Gma.Modules.TaskRuntime.Application.Commands;
using Gma.Modules.TaskRuntime.Application.Queries;
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

        group.MapGet("/schedules", async (
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
                    new ListRetentionScheduleHealthQuery(),
                    token),
                cancellationToken).ConfigureAwait(false));

        group.MapPost("/runs/{runId:guid}/retry", async (
            Guid runId,
            RetryRetentionRunRequest request,
            HttpContext context,
            AdminApiExecutor executor,
            IRequestDispatcher dispatcher,
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
                        dispatcher,
                        token)
                    : Task.FromResult(
                        Result.Failure<Unit>(
                            AdminErrors.ConfirmationRequired)),
                cancellationToken).ConfigureAwait(false));
    }

    private static async Task<Result<Unit>> RetryAsync(
        Guid runId,
        DateTimeOffset? scheduledAtUtc,
        string actor,
        string? tenantId,
        IRequestDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        Result<TaskRunDetails> loaded = await dispatcher.QueryAsync(
            new GetTaskRunQuery(runId),
            cancellationToken).ConfigureAwait(false);
        if (loaded.IsFailure)
        {
            return Result.Failure<Unit>(loaded.Error);
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
            return Result.Failure<Unit>(
                RetentionApplicationErrors.TaskRunUnavailable);
        }

        return await dispatcher.SendAsync(
            new RetryTaskRunCommand(runId, actor, scheduledAtUtc),
            cancellationToken).ConfigureAwait(false);
    }

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

namespace BunkFy.Modules.DataRights.Api;

using System.Globalization;
using BunkFy.Modules.DataRights.Application.Models;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Domain.Models;
using Gma.Framework.AccessControl.AspNetCore;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

internal sealed class DataRightsDownloadAuditFilter : IEndpointFilter
{
    private static readonly object HandledKey = new();

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        object? result = await next(context).ConfigureAwait(false);
        if (context.HttpContext.Items.ContainsKey(HandledKey) ||
            result is not IStatusCodeHttpResult statusResult ||
            statusResult.StatusCode is not (
                StatusCodes.Status401Unauthorized or
                StatusCodes.Status403Forbidden))
        {
            return result;
        }

        HttpContext httpContext = context.HttpContext;
        IScopeContext scopeContext =
            httpContext.RequestServices.GetRequiredService<IScopeContext>();
        if (!scopeContext.IsEnabled ||
            string.IsNullOrWhiteSpace(scopeContext.ScopeId) ||
            !TryRouteId(httpContext, "caseId", out Guid caseId) ||
            !TryRouteId(httpContext, "artifactId", out Guid artifactId))
        {
            return result;
        }

        DataRightsCaseScope scope;
        if (httpContext.Request.RouteValues.ContainsKey("propertyId"))
        {
            if (!TryRouteId(httpContext, "propertyId", out Guid propertyId))
            {
                return result;
            }

            scope = DataRightsCaseScope.ForProperty(propertyId);
        }
        else
        {
            scope = DataRightsCaseScope.Staff;
        }

        IAccessHttpSubjectResolver subjectResolver =
            httpContext.RequestServices
                .GetRequiredService<IAccessHttpSubjectResolver>();
        string actor = DataRightsEndpointSupport.ResolveActor(
            httpContext,
            subjectResolver) ?? "system:unresolved-subject";
        ISystemClock clock =
            httpContext.RequestServices.GetRequiredService<ISystemClock>();
        IDataRightsExportAuditSink audit =
            httpContext.RequestServices
                .GetRequiredService<IDataRightsExportAuditSink>();
        await audit.RecordAsync(
            new DataRightsExportAuditFact(
                scopeContext.ScopeId,
                artifactId,
                caseId,
                scope.CaseType,
                scope.PropertyId,
                DataRightsExportAuditAction.Download,
                actor,
                $"denied-http-{statusResult.StatusCode.Value.ToString(
                    CultureInfo.InvariantCulture)}",
                clock.UtcNow),
            httpContext.RequestAborted).ConfigureAwait(false);
        return result;
    }

    public static void MarkHandled(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Items[HandledKey] = true;
    }

    private static bool TryRouteId(
        HttpContext context,
        string key,
        out Guid id) =>
        Guid.TryParse(
            Convert.ToString(
                context.Request.RouteValues[key],
                CultureInfo.InvariantCulture),
            out id) &&
        id != Guid.Empty;
}

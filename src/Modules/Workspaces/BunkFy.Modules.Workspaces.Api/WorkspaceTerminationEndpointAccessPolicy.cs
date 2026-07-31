namespace BunkFy.Modules.Workspaces.Api;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Workspaces.Contracts;
using Gma.Framework.Api.Observability;
using Gma.Framework.Api.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

internal sealed class WorkspaceTerminationEndpointAccessPolicy(
    IWorkspaceTerminationFenceReader fences,
    ILogger<WorkspaceTerminationEndpointAccessPolicy> logger)
    : ITenantEndpointAccessPolicy
{
    public async ValueTask<TenantEndpointAccessDecision> AuthorizeAsync(
        HttpContext httpContext,
        string tenantId,
        CancellationToken cancellationToken)
    {
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
                "Workspace termination admission state is unavailable because {ExceptionType} was raised.",
                exception.GetType().Name);
            return TenantEndpointAccessDecision.Denied(
                "Workspaces.TerminationAdmissionUnavailable",
                "Workspace admission state is temporarily unavailable.",
                StatusCodes.Status503ServiceUnavailable);
        }

        if (fence is null)
        {
            return TenantEndpointAccessDecision.Allowed;
        }

        if (!IsValid(fence))
        {
            logger.LogError(
                "Workspace termination admission state is invalid.");
            return TenantEndpointAccessDecision.Denied(
                "Workspaces.TerminationAdmissionUnavailable",
                "Workspace admission state is temporarily unavailable.",
                StatusCodes.Status503ServiceUnavailable);
        }

        return IsExactAllowedEndpoint(httpContext, fence)
            ? TenantEndpointAccessDecision.Allowed
            : TenantEndpointAccessDecision.Denied(
                "Workspaces.TerminationFenceActive",
                "The workspace is not accepting ordinary operations.",
                StatusCodes.Status423Locked);
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

    private static bool IsExactAllowedEndpoint(
        HttpContext httpContext,
        WorkspaceTerminationFenceSnapshot fence)
    {
        Endpoint? endpoint = httpContext.GetEndpoint();
        ModuleEndpointMetadata? module = endpoint?.Metadata
            .GetMetadata<ModuleEndpointMetadata>();
        WorkspaceTerminationAccessRequirement? requirement =
            endpoint?.Metadata
                .GetMetadata<WorkspaceTerminationAccessRequirement>();
        if (!string.Equals(
                module?.ModuleName,
                DataRightsModuleMetadata.Name,
                StringComparison.Ordinal) ||
            requirement is null ||
            requirement.Purpose ==
                WorkspaceTerminationAccessPurpose.Unknown ||
            string.IsNullOrWhiteSpace(requirement.ProcessRouteValueName) ||
            !TryReadProcessId(
                httpContext,
                requirement.ProcessRouteValueName,
                out Guid processId) ||
            processId != fence.ProcessId)
        {
            return false;
        }

        return fence.State switch
        {
            WorkspaceTerminationFenceState.Frozen =>
                requirement.Purpose is
                    WorkspaceTerminationAccessPurpose.Review or
                    WorkspaceTerminationAccessPurpose.Export or
                    WorkspaceTerminationAccessPurpose.Cancellation or
                    WorkspaceTerminationAccessPurpose.Recovery,
            WorkspaceTerminationFenceState.DestructionStarted or
                WorkspaceTerminationFenceState.Closed =>
                requirement.Purpose ==
                    WorkspaceTerminationAccessPurpose.Recovery,
            _ => false
        };
    }

    private static bool TryReadProcessId(
        HttpContext httpContext,
        string routeValueName,
        out Guid processId)
    {
        object? routeValue =
            httpContext.Request.RouteValues[routeValueName.Trim()];
        return routeValue switch
        {
            Guid value when value != Guid.Empty =>
                Assign(value, out processId),
            string value when Guid.TryParse(value, out Guid parsed) &&
                parsed != Guid.Empty =>
                Assign(parsed, out processId),
            _ => Assign(Guid.Empty, out processId, result: false)
        };
    }

    private static bool Assign(
        Guid value,
        out Guid target,
        bool result = true)
    {
        target = value;
        return result;
    }
}

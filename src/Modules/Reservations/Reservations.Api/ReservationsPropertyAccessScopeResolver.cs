namespace Reservations.Api;

using Gma.Framework.AccessControl;
using Gma.Framework.AccessControl.AspNetCore;
using Gma.Framework.Tenancy;
using Microsoft.AspNetCore.Http;

internal sealed class ReservationsPropertyAccessScopeResolver(ITenantContext tenantContext) : IAccessHttpScopeResolver
{
    public const string ResolverName = "reservations-property";

    public string Name => ResolverName;

    public ValueTask<AccessScopeResolutionResult> ResolveAsync(
        HttpContext httpContext,
        AccessPermissionMetadata metadata,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(metadata);
        if (string.IsNullOrWhiteSpace(tenantContext.TenantId))
        {
            return ValueTask.FromResult(AccessScopeResolutionResult.Failure(
                AccessControlHttpErrorCodes.ScopeRequired,
                "A tenant access scope is required.",
                StatusCodes.Status400BadRequest));
        }

        if (!Guid.TryParse(httpContext.Request.RouteValues["propertyId"]?.ToString(), out Guid propertyId))
        {
            return ValueTask.FromResult(AccessScopeResolutionResult.Failure(
                AccessControlHttpErrorCodes.ScopeInvalid,
                "A valid property access scope is required.",
                StatusCodes.Status400BadRequest));
        }

        AccessScope scope = AccessScope.Create(
            AccessScopeSegment.Create("tenant", tenantContext.TenantId),
            AccessScopeSegment.Create("property", propertyId.ToString("D")));
        return ValueTask.FromResult(AccessScopeResolutionResult.Success(scope));
    }
}

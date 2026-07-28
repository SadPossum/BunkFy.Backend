namespace BunkFy.Host.AdminApi.Security;

using System.Globalization;
using BunkFy.Modules.Workspaces.Contracts;
using Gma.Framework.Administration;
using Gma.Framework.Administration.Api;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

public sealed class BunkFyAdminApiResourceScopeResolver : IAdminApiResourceScopeResolver
{
    private const string PropertyRouteValueName = "propertyId";

    public bool TryResolve(
        HttpContext httpContext,
        out AdminResourceScope? resourceScope)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        resourceScope = null;

        if (!httpContext.Request.RouteValues.TryGetValue(PropertyRouteValueName, out object? routeValue))
        {
            return true;
        }

        string? propertyIdValue = Convert.ToString(routeValue, CultureInfo.InvariantCulture);
        if (!Guid.TryParse(propertyIdValue, out Guid propertyId) ||
            propertyId == Guid.Empty)
        {
            return false;
        }

        resourceScope = AdminResourceScope.Create(
            AdminResourceScopeSegment.Create(
                WorkspaceAccessScopes.PropertySegmentName,
                propertyId.ToString("D")));
        return true;
    }
}

public static class BunkFyAdminApiResourceScopeRegistration
{
    public static IServiceCollection AddBunkFyAdminApiResourceScopes(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.Replace(ServiceDescriptor.Singleton<
            IAdminApiResourceScopeResolver,
            BunkFyAdminApiResourceScopeResolver>());
        return services;
    }
}

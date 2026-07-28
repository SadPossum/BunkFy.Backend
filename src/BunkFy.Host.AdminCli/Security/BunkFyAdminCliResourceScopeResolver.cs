namespace BunkFy.Host.AdminCli.Security;

using BunkFy.Modules.Workspaces.Contracts;
using Gma.Framework.Administration;
using Gma.Framework.Administration.Cli;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.CommandLine;

public sealed class BunkFyAdminCliResourceScopeResolver : IAdminCliResourceScopeResolver
{
    private const string PropertyOptionName = "--property-id";

    public bool TryResolve(
        ParseResult parseResult,
        out AdminResourceScope? resourceScope)
    {
        ArgumentNullException.ThrowIfNull(parseResult);
        resourceScope = null;

        Guid propertyId;
        try
        {
            propertyId = parseResult.GetValue<Guid>(PropertyOptionName);
        }
        catch (ArgumentException)
        {
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (InvalidCastException)
        {
            return false;
        }

        if (propertyId == Guid.Empty)
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

public static class BunkFyAdminCliResourceScopeRegistration
{
    public static IServiceCollection AddBunkFyAdminCliResourceScopes(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.Replace(ServiceDescriptor.Singleton<
            IAdminCliResourceScopeResolver,
            BunkFyAdminCliResourceScopeResolver>());
        return services;
    }
}

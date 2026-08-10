namespace BunkFy.Host.AdminCli.Security;

using BunkFy.Modules.Workspaces.Contracts;
using Gma.Framework.Administration;
using Gma.Framework.Administration.Cli;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.CommandLine;
using System.CommandLine.Parsing;

public sealed class BunkFyAdminCliResourceScopeResolver : IAdminCliResourceScopeResolver
{
    private const string PropertyOptionName = "--property-id";

    public bool TryResolve(
        ParseResult parseResult,
        out AdminResourceScope? resourceScope)
    {
        ArgumentNullException.ThrowIfNull(parseResult);
        resourceScope = null;

        SymbolResult? propertyResult;
        try
        {
            propertyResult = parseResult.GetResult(PropertyOptionName);
        }
        catch (ArgumentException)
        {
            return true;
        }

        if (propertyResult is null)
        {
            return true;
        }

        if (propertyResult is not OptionResult
            {
                Option: Option<Guid> typedPropertyOption
            })
        {
            return false;
        }

        Guid propertyId;
        try
        {
            propertyId = parseResult.GetRequiredValue(typedPropertyOption);
        }
        catch (InvalidOperationException)
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

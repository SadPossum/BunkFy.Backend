namespace BunkFy.Extensions.Workspaces;

using BunkFy.Modules.Staff.Contracts;
using Gma.Framework.Messaging;
using Gma.Modules.AccessControl.Contracts;
using Gma.Modules.Organizations.Contracts;
using Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    public static IServiceCollection AddBunkFyWorkspaces(
        this IServiceCollection services,
        Action<BunkFyWorkspacesOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (configure is not null)
        {
            services.Configure(configure);
        }
        else
        {
            services.AddOptions<BunkFyWorkspacesOptions>();
        }

        services.AddOptions<BunkFyWorkspacesOptions>()
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.GlobalAuthScopeId),
                "A global Auth scope id is required.")
            .ValidateOnStart();

        services.AddIntegrationEventHandler<
            OrganizationMembershipChangedIntegrationEvent,
            OrganizationMembershipStaffHandler>(
            StaffModuleMetadata.Name,
            OrganizationsModuleMetadata.Name);
        services.AddIntegrationEventHandler<
            OrganizationMembershipChangedIntegrationEvent,
            OrganizationMembershipAccessHandler>(
            AccessControlModuleMetadata.Name,
            OrganizationsModuleMetadata.Name);

        return services;
    }
}

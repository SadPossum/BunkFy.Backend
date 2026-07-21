namespace BunkFy.Modules.Workspaces.Application;

using BunkFy.Modules.Workspaces.Application.Handlers;
using BunkFy.Modules.Workspaces.Contracts;
using Gma.Framework.Application.Composition;
using Gma.Framework.Messaging;
using Gma.Modules.Organizations.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

public static class DependencyInjection
{
    public static IServiceCollection AddWorkspacesApplication(
        this IServiceCollection services,
        string globalAuthScopeId)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(globalAuthScopeId);

        services.Configure<WorkspaceStaffOnboardingOptions>(
            options => options.GlobalAuthScopeId = globalAuthScopeId.Trim());
        services.AddApplicationServicesFromAssembly(typeof(DependencyInjection).Assembly);
        services.TryAddScoped<WorkspaceStaffOnboardingProcessor>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<
            IOrganizationJoinAdmissionPolicy,
            WorkspaceStaffJoinAdmissionPolicy>());
        services.AddIntegrationEventHandler<
            OrganizationInvitationChangedIntegrationEvent,
            OrganizationInvitationStaffOnboardingHandler>(
            WorkspacesModuleMetadata.Name,
            OrganizationsModuleMetadata.Name);
        services.AddIntegrationEventHandler<
            OrganizationEnrollmentClaimChangedIntegrationEvent,
            OrganizationEnrollmentClaimStaffOnboardingHandler>(
            WorkspacesModuleMetadata.Name,
            OrganizationsModuleMetadata.Name);
        services.AddIntegrationEventHandler<
            OrganizationEnrollmentLinkChangedIntegrationEvent,
            OrganizationEnrollmentLinkStaffOnboardingHandler>(
            WorkspacesModuleMetadata.Name,
            OrganizationsModuleMetadata.Name);
        return services;
    }
}

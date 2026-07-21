namespace BunkFy.Modules.Workspaces.Application;

using BunkFy.Modules.Properties.Contracts;
using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Workspaces.Application.Handlers;
using BunkFy.Modules.Workspaces.Application.Tasks;
using BunkFy.Modules.Workspaces.Contracts;
using Gma.Framework.Application.Composition;
using Gma.Framework.Messaging;
using Gma.Framework.ProjectionRebuild.Tasks;
using Gma.Framework.Tasks;
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
        services.TryAddScoped<WorkspaceStaffAccessPlanPolicy>();
        services.TryAddScoped<
            IWorkspaceStaffJoinSourceIssuer,
            WorkspaceStaffJoinSourceIssuer>();
        services.TryAddScoped<WorkspaceAccessProvisioner>();
        services.TryAddScoped<WorkspaceStaffAccessDenier>();
        services.TryAddScoped<WorkspaceStaffAccessRestorer>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<
            IStaffLifecyclePolicy,
            WorkspaceStaffLifecyclePolicy>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<
            IOrganizationMembershipChangePolicy,
            WorkspaceOrganizationMembershipChangePolicy>());
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
        services.AddIntegrationEventHandler<
            OrganizationMembershipChangedIntegrationEvent,
            OrganizationMembershipAccessProfileSeedHandler>(
            WorkspacesModuleMetadata.Name,
            OrganizationsModuleMetadata.Name);
        services.AddIntegrationEventHandler<
            StaffMemberLifecycleChangedIntegrationEvent,
            StaffLifecycleWorkspaceAccessHandler>(
            WorkspacesModuleMetadata.Name,
            StaffModuleMetadata.Name);
        services.AddIntegrationEventHandler<
            PropertyCreatedIntegrationEvent,
            WorkspacePropertyCreatedHandler>(
            WorkspacesModuleMetadata.Name,
            PropertiesModuleMetadata.Name);
        services.AddIntegrationEventHandler<
            PropertyUpdatedIntegrationEvent,
            WorkspacePropertyUpdatedHandler>(
            WorkspacesModuleMetadata.Name,
            PropertiesModuleMetadata.Name);
        services.AddIntegrationEventHandler<
            PropertyRetiredIntegrationEvent,
            WorkspacePropertyRetiredHandler>(
            WorkspacesModuleMetadata.Name,
            PropertiesModuleMetadata.Name);
        return services;
    }

    public static IServiceCollection AddWorkspacesTaskHandlers(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddProjectionRebuildTasks();
        services.AddTaskHandler<
            RebuildWorkspacePropertiesPayload,
            RebuildWorkspacePropertiesTaskHandler>(WorkspacesModuleMetadata.Name);
        return services;
    }
}

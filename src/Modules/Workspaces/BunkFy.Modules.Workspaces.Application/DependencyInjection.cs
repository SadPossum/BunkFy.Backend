namespace BunkFy.Modules.Workspaces.Application;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Guests.Contracts;
using BunkFy.Modules.Ingestion.Contracts;
using BunkFy.Modules.Inventory.Contracts;
using BunkFy.Modules.Properties.Contracts;
using BunkFy.Modules.Reservations.Contracts;
using BunkFy.Modules.Retention.Contracts;
using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Workspaces.Application.Authorization;
using BunkFy.Modules.Workspaces.Application.Contributors;
using BunkFy.Modules.Workspaces.Application.Handlers;
using BunkFy.Modules.Workspaces.Application.Tasks;
using BunkFy.Modules.Workspaces.Contracts;
using Gma.Framework.AccessControl;
using Gma.Framework.Application.Composition;
using Gma.Framework.Messaging;
using Gma.Framework.ProjectionRebuild.Tasks;
using Gma.Framework.Tasks;
using Gma.Modules.Organizations.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

public static class DependencyInjection
{
    public static IServiceCollection AddWorkspacesApplication(
        this IServiceCollection services,
        IConfiguration configuration,
        string globalAuthScopeId)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(globalAuthScopeId);

        services.Configure<WorkspaceStaffOnboardingOptions>(
            options => options.GlobalAuthScopeId = globalAuthScopeId.Trim());
        services.AddOptions<WorkspaceStaffOnboardingRetentionOptions>()
            .Bind(configuration.GetSection(
                WorkspaceStaffOnboardingRetentionOptions.SectionName))
            .ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<
            IValidateOptions<WorkspaceStaffOnboardingRetentionOptions>,
            WorkspaceStaffOnboardingRetentionOptionsValidator>());
        AddDelegatedPermissionPolicies(services);
        services.AddApplicationServicesFromAssembly(typeof(DependencyInjection).Assembly);
        services.TryAddScoped<IWorkspaceAuthoritativeScope, WorkspaceAuthoritativeScope>();
        services.TryAddScoped<WorkspaceStaffJoinTokenAuthorityResolver>();
        services.TryAddScoped<
            IWorkspaceStaffOnboardingSubmitter,
            WorkspaceStaffOnboardingSubmitter>();
        services.TryAddScoped<WorkspaceStaffOnboardingProcessor>();
        services.TryAddScoped<
            WorkspaceStaffOnboardingDataRightsCorrectionAuthorizer>();
        services.TryAddScoped<WorkspaceStaffAccessPlanPolicy>();
        services.TryAddScoped<
            IWorkspaceStaffJoinSourceIssuer,
            WorkspaceStaffJoinSourceIssuer>();
        services.TryAddScoped<
            IWorkspaceStaffJoinSourceManager,
            WorkspaceStaffJoinSourceManager>();
        services.TryAddScoped<
            IWorkspaceStaffJoinSourceReplacementManager,
            WorkspaceStaffJoinSourceReplacementManager>();
        services.TryAddScoped<
            IWorkspaceAccessProfileManager,
            WorkspaceAccessProfileManager>();
        services.TryAddScoped<
            IWorkspaceMemberAccessManager,
            WorkspaceMemberAccessManager>();
        services.TryAddScoped<WorkspaceAccessProvisioner>();
        services.TryAddScoped<WorkspaceStaffAccessDenier>();
        services.TryAddScoped<WorkspaceStaffAccessRestorer>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<
            IStaffLifecyclePolicy,
            WorkspaceStaffLifecyclePolicy>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<
            IDataRightsAnonymisationExecutionPrerequisiteV2,
            WorkspaceStaffAnonymisationAccessPrerequisite>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<
            IDataRightsAnonymisationRestorePrerequisiteV3,
            WorkspaceStaffAnonymisationAccessPrerequisite>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<
            IStaffRetentionAnonymisationPrerequisite,
            WorkspaceStaffAnonymisationAccessPrerequisite>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<
            IOrganizationMembershipChangePolicy,
            WorkspaceOrganizationMembershipChangePolicy>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<
            IOrganizationJoinAdmissionPolicy,
            WorkspaceStaffJoinAdmissionPolicy>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<
            IRetentionExecutionContributor,
            WorkspaceStaffOnboardingRetentionContributor>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<
            IDataRightsCorrectionPolicyContributor,
            WorkspaceStaffOnboardingDataRightsCorrectionPolicyContributor>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<
            IDataRightsRestrictionContributor,
            WorkspaceStaffOnboardingDataRightsRestrictionContributor>());
        services.AddIntegrationEventHandler<
            OrganizationInvitationChangedIntegrationEvent,
            OrganizationInvitationStaffOnboardingHandler>(
            WorkspacesModuleMetadata.Name,
            OrganizationsModuleMetadata.Name);
        services.AddIntegrationEventHandler<
            OrganizationInvitationExpiredIntegrationEvent,
            OrganizationInvitationExpiredStaffOnboardingHandler>(
            WorkspacesModuleMetadata.Name,
            OrganizationsModuleMetadata.Name);
        services.AddIntegrationEventHandler<
            OrganizationEnrollmentClaimChangedIntegrationEvent,
            OrganizationEnrollmentClaimStaffOnboardingHandler>(
            WorkspacesModuleMetadata.Name,
            OrganizationsModuleMetadata.Name);
        services.AddIntegrationEventHandler<
            OrganizationEnrollmentClaimExpiredIntegrationEvent,
            OrganizationEnrollmentClaimExpiredStaffOnboardingHandler>(
            WorkspacesModuleMetadata.Name,
            OrganizationsModuleMetadata.Name);
        services.AddIntegrationEventHandler<
            OrganizationEnrollmentLinkChangedIntegrationEvent,
            OrganizationEnrollmentLinkStaffOnboardingHandler>(
            WorkspacesModuleMetadata.Name,
            OrganizationsModuleMetadata.Name);
        services.AddIntegrationEventHandler<
            OrganizationEnrollmentLinkExpiredIntegrationEvent,
            OrganizationEnrollmentLinkExpiredStaffOnboardingHandler>(
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
        services.AddIntegrationEventHandler<
            WorkspaceStaffOnboardingProcessingRestrictionChangedIntegrationEvent,
            WorkspaceStaffOnboardingProcessingRestrictionRecoveryHandler>(
            WorkspacesModuleMetadata.Name,
            WorkspacesModuleMetadata.Name);
        return services;
    }

    private static void AddDelegatedPermissionPolicies(IServiceCollection services)
    {
        services.AddGmaAccessControlPermissionPolicies(PropertiesModuleMetadata.Descriptor);
        services.AddGmaAccessControlPermissionPolicies(InventoryModuleMetadata.Descriptor);
        services.AddGmaAccessControlPermissionPolicies(ReservationsModuleMetadata.Descriptor);
        services.AddGmaAccessControlPermissionPolicies(GuestsModuleMetadata.Descriptor);
        services.AddGmaAccessControlPermissionPolicies(StaffModuleMetadata.Descriptor);
        services.AddGmaAccessControlPermissionPolicies(IngestionModuleMetadata.Descriptor);
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

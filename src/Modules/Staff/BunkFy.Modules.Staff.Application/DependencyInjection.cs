namespace BunkFy.Modules.Staff.Application;

using BunkFy.DataGovernance;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Staff.Application.Contributors;
using Gma.Framework.AccessControl;
using Gma.Framework.Application.Composition;
using Gma.Framework.Messaging;
using Gma.Framework.ProjectionRebuild.Tasks;
using Gma.Framework.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using BunkFy.Modules.Properties.Contracts;
using BunkFy.Modules.Staff.Application.Handlers;
using BunkFy.Modules.Staff.Application.Tasks;
using BunkFy.Modules.Staff.Contracts;

public static class DependencyInjection
{
    public static IServiceCollection AddStaffApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton(_ => CountryPolicyRegistry.Create(
            [],
            [],
            CountryPolicyRuntimeMode.Engineering));
        services.AddApplicationServicesFromAssembly(typeof(DependencyInjection).Assembly);
        services.TryAddScoped<IStaffIdentityReconciler, StaffIdentityReconciler>();
        services.TryAddScoped<IStaffOnboardingProvisioner, StaffOnboardingProvisioner>();
        services.TryAddScoped<
            IStaffPropertyAssignmentProvisioner,
            StaffPropertyAssignmentProvisioner>();
        services.TryAddScoped<StaffLifecyclePolicyEvaluator>();
        services.TryAddEnumerable(
            ServiceDescriptor.Scoped<
                IDataRightsCorrectionPolicyContributor,
                StaffDataRightsCorrectionPolicyContributor>());
        services.TryAddEnumerable(
            ServiceDescriptor.Scoped<
                IDataRightsRestrictionContributor,
                StaffDataRightsRestrictionContributor>());
        services.TryAddEnumerable(
            ServiceDescriptor.Scoped<
                IDataRightsAnonymisationPolicyContributor,
                StaffDataRightsAnonymisationPolicyContributor>());
        services.AddGmaAccessControlPermissionPolicies(StaffModuleMetadata.Descriptor);
        services.AddIntegrationEventHandler<PropertyCreatedIntegrationEvent, StaffPropertyCreatedHandler>(
            StaffModuleMetadata.Name, PropertiesModuleMetadata.Name);
        services.AddIntegrationEventHandler<PropertyUpdatedIntegrationEvent, StaffPropertyUpdatedHandler>(
            StaffModuleMetadata.Name, PropertiesModuleMetadata.Name);
        services.AddIntegrationEventHandler<PropertyRetiredIntegrationEvent, StaffPropertyRetiredHandler>(
            StaffModuleMetadata.Name, PropertiesModuleMetadata.Name);
        return services;
    }

    public static IServiceCollection AddStaffTaskHandlers(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddProjectionRebuildTasks();
        services.AddTaskHandler<RebuildStaffPropertiesPayload, RebuildStaffPropertiesTaskHandler>(
            StaffModuleMetadata.Name);
        return services;
    }
}

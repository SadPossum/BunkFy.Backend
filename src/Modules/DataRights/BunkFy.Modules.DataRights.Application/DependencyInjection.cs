namespace BunkFy.Modules.DataRights.Application;

using BunkFy.DataGovernance;
using BunkFy.Modules.DataRights.Application.Handlers;
using BunkFy.Modules.DataRights.Application.Policies;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Application.Tasks;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Contracts.Authorization;
using BunkFy.Modules.DataRights.Application.Authorization;
using BunkFy.Modules.Properties.Contracts;
using Gma.Framework.AccessControl;
using Gma.Framework.Application.Composition;
using Gma.Framework.Messaging;
using Gma.Framework.ProjectionRebuild.Tasks;
using Gma.Framework.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

public static class DependencyInjection
{
    public static IServiceCollection AddDataRightsApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddApplicationServicesFromAssembly(typeof(DependencyInjection).Assembly);
        services.AddScoped<IDataRightsOperationApprovalGate, DataRightsOperationApprovalGate>();
        services.TryAddSingleton(_ => CountryPolicyRegistry.Create(
            [],
            [],
            CountryPolicyRuntimeMode.Engineering));
        services.TryAddScoped<
            IDataRightsAnonymisationApprovalPolicy,
            DataRightsAnonymisationApprovalPolicy>();
        services.AddIntegrationEventHandler<
            PropertyCreatedIntegrationEvent,
            DataRightsPropertyCreatedHandler>(
                DataRightsModuleMetadata.Name,
                PropertiesModuleMetadata.Name);
        services.AddIntegrationEventHandler<
            PropertyUpdatedIntegrationEvent,
            DataRightsPropertyUpdatedHandler>(
                DataRightsModuleMetadata.Name,
                PropertiesModuleMetadata.Name);
        services.AddIntegrationEventHandler<
            PropertyRetiredIntegrationEvent,
            DataRightsPropertyRetiredHandler>(
                DataRightsModuleMetadata.Name,
                PropertiesModuleMetadata.Name);
        services.AddIntegrationEventHandler<
            PropertyProcessingPolicyActivatedIntegrationEvent,
            DataRightsPropertyProcessingPolicyActivatedHandler>(
                DataRightsModuleMetadata.Name,
                PropertiesModuleMetadata.Name);
        services.AddIntegrationEventHandler<
            PropertyProcessingSuspendedIntegrationEvent,
            DataRightsPropertyProcessingSuspendedHandler>(
                DataRightsModuleMetadata.Name,
                PropertiesModuleMetadata.Name);
        services.AddGmaAccessControlPermissionPolicies(DataRightsModuleMetadata.Descriptor);
        return services;
    }

    public static IServiceCollection AddDataRightsTaskHandlers(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddProjectionRebuildTasks();
        services.AddTaskHandler<
            RebuildDataRightsPropertiesPayload,
            RebuildDataRightsPropertiesTaskHandler>(DataRightsModuleMetadata.Name);
        return services;
    }
}

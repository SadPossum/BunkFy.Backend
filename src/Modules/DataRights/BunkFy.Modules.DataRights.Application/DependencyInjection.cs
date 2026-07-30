namespace BunkFy.Modules.DataRights.Application;

using BunkFy.DataGovernance;
using BunkFy.Modules.DataRights.Application.Authorization;
using BunkFy.Modules.DataRights.Application.Handlers;
using BunkFy.Modules.DataRights.Application.Policies;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Application.Security;
using BunkFy.Modules.DataRights.Application.Tasks;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Contracts.Authorization;
using BunkFy.Modules.Properties.Contracts;
using Gma.Framework.AccessControl;
using Gma.Framework.Application.Composition;
using Gma.Framework.Messaging;
using Gma.Framework.Observability;
using Gma.Framework.ProjectionRebuild.Tasks;
using Gma.Framework.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

public static class DependencyInjection
{
    public static IServiceCollection AddDataRightsApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSecuritySignalCore();
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<
                ISecuritySignalDefinitionSource,
                DataRightsApprovalSecuritySignalDefinitions>());
        services.AddApplicationServicesFromAssembly(typeof(DependencyInjection).Assembly);
        services.AddScoped<IDataRightsOperationApprovalGate, DataRightsOperationApprovalGate>();
        services.AddScoped<
            IDataRightsCorrectionExecutionGate,
            DataRightsCorrectionExecutionGate>();
        services.TryAddScoped<DataRightsCorrectionCompletionCoordinator>();
        services.TryAddSingleton(_ => CountryPolicyRegistry.Create(
            [],
            [],
            CountryPolicyRuntimeMode.Engineering));
        services.TryAddScoped<
            IDataRightsAnonymisationApprovalPolicy,
            DataRightsAnonymisationApprovalPolicy>();
        services.TryAddScoped<DataRightsRequiredCompanionExpander>();
        services.TryAddScoped<
            IDataRightsRestoreCoordinator,
            DataRightsRestoreCoordinator>();
        services.TryAddScoped<
            IDataRightsExportAssembler,
            DataRightsExportAssembler>();
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
        services.AddIntegrationEventHandler<
            DataRightsCorrectionAppliedIntegrationEvent,
            GuestDataRightsCorrectionAppliedHandler>(
                DataRightsModuleMetadata.Name,
                DataRightsModuleMetadata.GuestsProducerModuleName);
        services.AddIntegrationEventHandler<
            DataRightsCorrectionAppliedIntegrationEvent,
            ReservationDataRightsCorrectionAppliedHandler>(
                DataRightsModuleMetadata.Name,
                DataRightsModuleMetadata.ReservationsProducerModuleName);
        services.AddIntegrationEventHandler<
            DataRightsTenantCorrectionAppliedIntegrationEvent,
            StaffDataRightsCorrectionAppliedHandler>(
                DataRightsModuleMetadata.Name,
                DataRightsModuleMetadata.StaffProducerModuleName);
        services.AddIntegrationEventHandler<
            DataRightsTenantCorrectionAppliedIntegrationEvent,
            WorkspacesDataRightsCorrectionAppliedHandler>(
                DataRightsModuleMetadata.Name,
                DataRightsModuleMetadata.WorkspacesProducerModuleName);
        services.AddGmaAccessControlPermissionPolicies(DataRightsModuleMetadata.Descriptor);
        return services;
    }

    public static IServiceCollection AddDataRightsTaskHandlers(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddProjectionRebuildTasks();
        services.TryAddScoped<
            DataRightsAnonymisationExecutionReconciler>();
        services.AddTaskHandler<
            RebuildDataRightsPropertiesPayload,
            RebuildDataRightsPropertiesTaskHandler>(DataRightsModuleMetadata.Name);
        services.AddIntegrationEventHandler<
            DataRightsAnonymisationExecutionPreparedIntegrationEvent,
            DataRightsAnonymisationExecutionPreparedHandler>(
                DataRightsModuleMetadata.Name,
                DataRightsModuleMetadata.Name);
        services.AddIntegrationEventHandler<
            DataRightsAnonymisationExecutionPreparedIntegrationEventV2,
            DataRightsAnonymisationExecutionPreparedV2Handler>(
                DataRightsModuleMetadata.Name,
                DataRightsModuleMetadata.Name);
        services.AddIntegrationEventHandler<
            DataRightsAnonymisationWorkItemTerminalIntegrationEvent,
            DataRightsAnonymisationWorkItemTerminalHandler>(
                DataRightsModuleMetadata.Name,
                DataRightsModuleMetadata.Name);
        services.AddIntegrationEventHandler<
            DataRightsAnonymisationWorkItemTerminalIntegrationEventV2,
            DataRightsAnonymisationWorkItemTerminalV2Handler>(
                DataRightsModuleMetadata.Name,
                DataRightsModuleMetadata.Name);
        services.AddIntegrationEventHandler<
            DataRightsExportArtifactRequestedIntegrationEvent,
            DataRightsExportArtifactRequestedHandler>(
                DataRightsModuleMetadata.Name,
                DataRightsModuleMetadata.Name);
        services.AddTaskHandler<
            ExecuteDataRightsAnonymisationPayload,
            ExecuteDataRightsAnonymisationTaskHandler>(
                DataRightsModuleMetadata.Name);
        services.AddTaskHandler<
            ExecuteDataRightsAnonymisationPayloadV2,
            ExecuteDataRightsAnonymisationTaskV2Handler>(
                DataRightsModuleMetadata.Name);
        services.AddTaskHandler<
            GenerateDataRightsExportPayload,
            GenerateDataRightsExportTaskHandler>(
                DataRightsModuleMetadata.Name);
        services.AddTaskHandler<
            DeleteExpiredDataRightsExportArtifactPayload,
            DeleteExpiredDataRightsExportArtifactTaskHandler>(
                DataRightsModuleMetadata.Name);
        return services;
    }
}

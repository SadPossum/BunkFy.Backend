namespace BunkFy.Modules.Retention.Application.Handlers;

using BunkFy.Modules.Properties.Contracts;
using BunkFy.Modules.Retention.Application.Ports;
using BunkFy.Modules.Retention.Contracts;
using Gma.Framework.Messaging;
using Gma.Modules.Organizations.Contracts;

[IntegrationEventHandler(RetentionModuleMetadata.OrganizationChangedHandlerName)]
internal sealed class RetentionOrganizationChangedHandler(
    RetentionScopeMutationCoordinator mutations)
    : IIntegrationEventHandler<OrganizationChangedIntegrationEvent>
{
    public Task HandleAsync(
        OrganizationChangedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken) =>
        mutations.ApplyOrganizationAsync(
            new(
                integrationEvent.ScopeId,
                integrationEvent.OrganizationId,
                integrationEvent.Status == OrganizationStatus.Active,
                integrationEvent.OrganizationVersion),
            cancellationToken);
}

[IntegrationEventHandler(RetentionModuleMetadata.PropertyCreatedHandlerName)]
internal sealed class RetentionPropertyCreatedHandler(
    RetentionScopeMutationCoordinator mutations)
    : IIntegrationEventHandler<PropertyCreatedIntegrationEvent>
{
    public Task HandleAsync(
        PropertyCreatedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken) =>
        mutations.ApplyPropertyTopologyAsync(
            new(
                integrationEvent.ScopeId,
                integrationEvent.PropertyId,
                integrationEvent.Status == PropertyStatus.Active,
                integrationEvent.PropertyVersion),
            cancellationToken);
}

[IntegrationEventHandler(RetentionModuleMetadata.PropertyUpdatedHandlerName)]
internal sealed class RetentionPropertyUpdatedHandler(
    RetentionScopeMutationCoordinator mutations)
    : IIntegrationEventHandler<PropertyUpdatedIntegrationEvent>
{
    public Task HandleAsync(
        PropertyUpdatedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken) =>
        mutations.ApplyPropertyTopologyAsync(
            new(
                integrationEvent.ScopeId,
                integrationEvent.PropertyId,
                integrationEvent.Status == PropertyStatus.Active,
                integrationEvent.PropertyVersion),
            cancellationToken);
}

[IntegrationEventHandler(RetentionModuleMetadata.PropertyRetiredHandlerName)]
internal sealed class RetentionPropertyRetiredHandler(
    RetentionScopeMutationCoordinator mutations)
    : IIntegrationEventHandler<PropertyRetiredIntegrationEvent>
{
    public Task HandleAsync(
        PropertyRetiredIntegrationEvent integrationEvent,
        CancellationToken cancellationToken) =>
        mutations.ApplyPropertyTopologyAsync(
            new(
                integrationEvent.ScopeId,
                integrationEvent.PropertyId,
                false,
                integrationEvent.PropertyVersion),
            cancellationToken);
}

[IntegrationEventHandler(
    RetentionModuleMetadata.PropertyProcessingPolicyActivatedHandlerName)]
internal sealed class RetentionPropertyProcessingPolicyActivatedHandler(
    RetentionScopeMutationCoordinator mutations)
    : IIntegrationEventHandler<PropertyProcessingPolicyActivatedIntegrationEvent>
{
    public Task HandleAsync(
        PropertyProcessingPolicyActivatedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken) =>
        mutations.ApplyPropertyPolicyAsync(
            new(
                integrationEvent.ScopeId,
                integrationEvent.PropertyId,
                true,
                integrationEvent.Binding.RetentionPolicyVersion,
                integrationEvent.PropertyVersion),
            cancellationToken);
}

[IntegrationEventHandler(
    RetentionModuleMetadata.PropertyProcessingSuspendedHandlerName)]
internal sealed class RetentionPropertyProcessingSuspendedHandler(
    RetentionScopeMutationCoordinator mutations)
    : IIntegrationEventHandler<PropertyProcessingSuspendedIntegrationEvent>
{
    public Task HandleAsync(
        PropertyProcessingSuspendedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken) =>
        mutations.ApplyPropertyPolicyAsync(
            new(
                integrationEvent.ScopeId,
                integrationEvent.PropertyId,
                false,
                integrationEvent.Binding.RetentionPolicyVersion,
                integrationEvent.PropertyVersion),
            cancellationToken);
}

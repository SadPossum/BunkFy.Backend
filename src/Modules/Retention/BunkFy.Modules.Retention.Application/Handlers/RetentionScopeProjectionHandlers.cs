namespace BunkFy.Modules.Retention.Application.Handlers;

using BunkFy.Modules.Properties.Contracts;
using BunkFy.Modules.Retention.Application.Ports;
using BunkFy.Modules.Retention.Contracts;
using Gma.Framework.Messaging;
using Gma.Modules.Organizations.Contracts;

[IntegrationEventHandler(RetentionModuleMetadata.OrganizationChangedHandlerName)]
internal sealed class RetentionOrganizationChangedHandler(
    IRetentionScopeRepository scopes)
    : IIntegrationEventHandler<OrganizationChangedIntegrationEvent>
{
    public Task HandleAsync(
        OrganizationChangedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken) =>
        scopes.ApplyOrganizationAsync(
            new(
                integrationEvent.ScopeId,
                integrationEvent.OrganizationId,
                integrationEvent.Status == OrganizationStatus.Active,
                integrationEvent.OrganizationVersion),
            cancellationToken);
}

[IntegrationEventHandler(RetentionModuleMetadata.PropertyCreatedHandlerName)]
internal sealed class RetentionPropertyCreatedHandler(IRetentionScopeRepository scopes)
    : IIntegrationEventHandler<PropertyCreatedIntegrationEvent>
{
    public Task HandleAsync(
        PropertyCreatedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken) =>
        scopes.ApplyPropertyTopologyAsync(
            new(
                integrationEvent.ScopeId,
                integrationEvent.PropertyId,
                integrationEvent.Status == PropertyStatus.Active,
                integrationEvent.PropertyVersion),
            cancellationToken);
}

[IntegrationEventHandler(RetentionModuleMetadata.PropertyUpdatedHandlerName)]
internal sealed class RetentionPropertyUpdatedHandler(IRetentionScopeRepository scopes)
    : IIntegrationEventHandler<PropertyUpdatedIntegrationEvent>
{
    public Task HandleAsync(
        PropertyUpdatedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken) =>
        scopes.ApplyPropertyTopologyAsync(
            new(
                integrationEvent.ScopeId,
                integrationEvent.PropertyId,
                integrationEvent.Status == PropertyStatus.Active,
                integrationEvent.PropertyVersion),
            cancellationToken);
}

[IntegrationEventHandler(RetentionModuleMetadata.PropertyRetiredHandlerName)]
internal sealed class RetentionPropertyRetiredHandler(IRetentionScopeRepository scopes)
    : IIntegrationEventHandler<PropertyRetiredIntegrationEvent>
{
    public Task HandleAsync(
        PropertyRetiredIntegrationEvent integrationEvent,
        CancellationToken cancellationToken) =>
        scopes.ApplyPropertyTopologyAsync(
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
    IRetentionScopeRepository scopes)
    : IIntegrationEventHandler<PropertyProcessingPolicyActivatedIntegrationEvent>
{
    public Task HandleAsync(
        PropertyProcessingPolicyActivatedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken) =>
        scopes.ApplyPropertyPolicyAsync(
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
    IRetentionScopeRepository scopes)
    : IIntegrationEventHandler<PropertyProcessingSuspendedIntegrationEvent>
{
    public Task HandleAsync(
        PropertyProcessingSuspendedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken) =>
        scopes.ApplyPropertyPolicyAsync(
            new(
                integrationEvent.ScopeId,
                integrationEvent.PropertyId,
                false,
                integrationEvent.Binding.RetentionPolicyVersion,
                integrationEvent.PropertyVersion),
            cancellationToken);
}

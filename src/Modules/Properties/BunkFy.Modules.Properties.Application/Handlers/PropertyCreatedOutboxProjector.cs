namespace BunkFy.Modules.Properties.Application.Handlers;

using BunkFy.Modules.Properties.Application.Mapping;
using BunkFy.Modules.Properties.Contracts;
using BunkFy.Modules.Properties.Domain.Events;
using Gma.Framework.Application.Events;
using Gma.Framework.Messaging;

internal sealed class PropertyCreatedOutboxProjector(IOutboxWriterRegistry outboxWriters)
    : IDomainEventHandler<PropertyCreatedDomainEvent>
{
    public Task HandleAsync(PropertyCreatedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        outboxWriters.GetRequired(PropertiesModuleMetadata.Name).EnqueueAsync(
            new PropertyCreatedIntegrationEvent(
                domainEvent.EventId,
                domainEvent.ScopeId,
                domainEvent.OccurredAtUtc,
                domainEvent.PropertyId,
                domainEvent.Name,
                domainEvent.Code,
                domainEvent.TimeZoneId,
                PropertiesMapper.MapStatus(domainEvent.Status),
                domainEvent.PropertyVersion),
            cancellationToken);
}

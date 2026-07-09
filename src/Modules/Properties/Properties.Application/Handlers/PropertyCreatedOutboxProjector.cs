namespace Properties.Application.Handlers;

using Properties.Application.Mapping;
using Properties.Contracts;
using Properties.Domain.Events;
using Gma.Framework.Application.Events;
using Gma.Framework.Messaging;

internal sealed class PropertyCreatedOutboxProjector(IOutboxWriterRegistry outboxWriters)
    : IDomainEventHandler<PropertyCreatedDomainEvent>
{
    public Task HandleAsync(PropertyCreatedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        outboxWriters.GetRequired(PropertiesModuleMetadata.Name).EnqueueAsync(
            new PropertyCreatedIntegrationEvent(
                domainEvent.EventId,
                domainEvent.TenantId,
                domainEvent.OccurredAtUtc,
                domainEvent.PropertyId,
                domainEvent.Name,
                domainEvent.Code,
                domainEvent.TimeZoneId,
                PropertiesMapper.MapStatus(domainEvent.Status)),
            cancellationToken);
}

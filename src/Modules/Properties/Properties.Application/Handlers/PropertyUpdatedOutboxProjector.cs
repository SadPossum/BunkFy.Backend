namespace Properties.Application.Handlers;

using Properties.Application.Mapping;
using Properties.Contracts;
using Properties.Domain.Events;
using Gma.Framework.Application.Events;
using Gma.Framework.Messaging;

internal sealed class PropertyUpdatedOutboxProjector(IOutboxWriterRegistry outboxWriters)
    : IDomainEventHandler<PropertyUpdatedDomainEvent>
{
    public Task HandleAsync(PropertyUpdatedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        outboxWriters.GetRequired(PropertiesModuleMetadata.Name).EnqueueAsync(
            new PropertyUpdatedIntegrationEvent(
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

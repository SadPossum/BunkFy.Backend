namespace BunkFy.Modules.Staff.Application.Handlers;

using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Staff.Domain.Events;
using Gma.Framework.Application.Events;
using Gma.Framework.Messaging;

internal sealed class StaffProcessingRestrictionOutboxProjector(
    IOutboxWriterRegistry outboxWriters)
    : IDomainEventHandler<StaffProcessingRestrictionChangedDomainEvent>
{
    public Task HandleAsync(
        StaffProcessingRestrictionChangedDomainEvent domainEvent,
        CancellationToken cancellationToken) =>
        outboxWriters.GetRequired(StaffModuleMetadata.Name).EnqueueAsync(
            new StaffProcessingRestrictionChangedIntegrationEvent(
                domainEvent.EventId,
                domainEvent.ScopeId,
                domainEvent.OccurredAtUtc,
                domainEvent.StaffMemberId,
                domainEvent.ContractVersion,
                domainEvent.ProjectionRevision,
                domainEvent.IsRestricted),
            cancellationToken);
}

namespace BunkFy.Modules.Staff.Application.Handlers;

using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Staff.Domain.Events;
using Gma.Framework.Application.Events;
using Gma.Framework.Messaging;

internal sealed class StaffMemberAnonymisedOutboxProjector(
    IOutboxWriterRegistry writers)
    : IDomainEventHandler<StaffMemberAnonymisedDomainEvent>
{
    public Task HandleAsync(
        StaffMemberAnonymisedDomainEvent domainEvent,
        CancellationToken cancellationToken) =>
        writers.GetRequired(StaffModuleMetadata.Name).EnqueueAsync(
            new StaffMemberAnonymisedIntegrationEvent(
                domainEvent.EventId,
                domainEvent.ScopeId,
                domainEvent.OccurredAtUtc,
                domainEvent.StaffMemberId,
                domainEvent.StaffVersion),
            cancellationToken);
}

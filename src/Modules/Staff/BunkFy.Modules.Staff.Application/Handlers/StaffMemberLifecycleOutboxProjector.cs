namespace BunkFy.Modules.Staff.Application.Handlers;

using Gma.Framework.Application.Events;
using Gma.Framework.Messaging;
using BunkFy.Modules.Staff.Application.Mapping;
using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Staff.Domain.Events;

internal sealed class StaffMemberLifecycleOutboxProjector(IOutboxWriterRegistry writers)
    : IDomainEventHandler<StaffMemberLifecycleChangedDomainEvent>
{
    public Task HandleAsync(StaffMemberLifecycleChangedDomainEvent e, CancellationToken cancellationToken) =>
        writers.GetRequired(StaffModuleMetadata.Name).EnqueueAsync(new StaffMemberLifecycleChangedIntegrationEvent(
            e.EventId, e.ScopeId, e.OccurredAtUtc, e.StaffMemberId,
            StaffMappings.MapStatus(e.Status), e.EffectiveOn, e.StaffVersion), cancellationToken);
}

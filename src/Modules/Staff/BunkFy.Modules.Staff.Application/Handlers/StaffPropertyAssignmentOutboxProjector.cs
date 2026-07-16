namespace BunkFy.Modules.Staff.Application.Handlers;

using Gma.Framework.Application.Events;
using Gma.Framework.Messaging;
using BunkFy.Modules.Staff.Application.Mapping;
using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Staff.Domain.Events;

internal sealed class StaffPropertyAssignmentOutboxProjector(IOutboxWriterRegistry writers)
    : IDomainEventHandler<StaffPropertyAssignmentChangedDomainEvent>
{
    public Task HandleAsync(StaffPropertyAssignmentChangedDomainEvent e, CancellationToken cancellationToken) =>
        writers.GetRequired(StaffModuleMetadata.Name).EnqueueAsync(new StaffPropertyAssignmentChangedIntegrationEvent(
            e.EventId, e.ScopeId, e.OccurredAtUtc, e.StaffMemberId, e.AssignmentId, e.PropertyId,
            e.IsCurrent, e.IsPrimary, e.EffectiveFrom, e.EffectiveTo, e.StaffVersion, e.ActorId), cancellationToken);
}

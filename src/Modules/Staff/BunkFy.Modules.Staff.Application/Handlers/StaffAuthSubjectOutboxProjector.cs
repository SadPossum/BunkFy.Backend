namespace BunkFy.Modules.Staff.Application.Handlers;

using Gma.Framework.Application.Events;
using Gma.Framework.Messaging;
using BunkFy.Modules.Staff.Application.Mapping;
using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Staff.Domain.Events;

internal sealed class StaffAuthSubjectOutboxProjector(IOutboxWriterRegistry writers)
    : IDomainEventHandler<StaffAuthSubjectChangedDomainEvent>
{
    public Task HandleAsync(StaffAuthSubjectChangedDomainEvent e, CancellationToken cancellationToken) =>
        writers.GetRequired(StaffModuleMetadata.Name).EnqueueAsync(new StaffAuthSubjectChangedIntegrationEvent(
            e.EventId, e.ScopeId, e.OccurredAtUtc, e.StaffMemberId,
            e.AuthSubjectId, e.StaffVersion), cancellationToken);
}

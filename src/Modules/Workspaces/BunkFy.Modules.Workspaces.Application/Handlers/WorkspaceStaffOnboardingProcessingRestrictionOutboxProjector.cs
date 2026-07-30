namespace BunkFy.Modules.Workspaces.Application.Handlers;

using BunkFy.Modules.Workspaces.Contracts;
using BunkFy.Modules.Workspaces.Domain.Events;
using Gma.Framework.Application.Events;
using Gma.Framework.Messaging;

internal sealed class
    WorkspaceStaffOnboardingProcessingRestrictionOutboxProjector(
        IOutboxWriterRegistry outboxWriters)
    : IDomainEventHandler<
        WorkspaceStaffOnboardingProcessingRestrictionChangedDomainEvent>
{
    public Task HandleAsync(
        WorkspaceStaffOnboardingProcessingRestrictionChangedDomainEvent
            domainEvent,
        CancellationToken cancellationToken) =>
        outboxWriters.GetRequired(WorkspacesModuleMetadata.Name).EnqueueAsync(
            new
                WorkspaceStaffOnboardingProcessingRestrictionChangedIntegrationEvent(
                    domainEvent.EventId,
                    domainEvent.ScopeId,
                    domainEvent.OccurredAtUtc,
                    domainEvent.ApplicationId,
                    domainEvent.ContractVersion,
                    domainEvent.ProjectionRevision,
                    domainEvent.IsRestricted),
            cancellationToken);
}

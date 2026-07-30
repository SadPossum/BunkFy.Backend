namespace BunkFy.Modules.Workspaces.Application.Handlers;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Workspaces.Application.Mapping;
using BunkFy.Modules.Workspaces.Contracts;
using BunkFy.Modules.Workspaces.Domain.DataRights;
using BunkFy.Modules.Workspaces.Domain.Events;
using Gma.Framework.Application.Events;
using Gma.Framework.Messaging;

internal sealed class
    WorkspaceStaffOnboardingDataRightsCorrectionAppliedOutboxProjector(
        IOutboxWriterRegistry outboxWriters)
    : IDomainEventHandler<
        WorkspaceStaffOnboardingCorrectionAppliedDomainEvent>
{
    public Task HandleAsync(
        WorkspaceStaffOnboardingCorrectionAppliedDomainEvent domainEvent,
        CancellationToken cancellationToken)
    {
        IOutboxWriter outbox =
            outboxWriters.GetRequired(WorkspacesModuleMetadata.Name);
        return outbox.EnqueueAsync(
            new DataRightsTenantCorrectionAppliedIntegrationEvent(
                domainEvent.EventId,
                domainEvent.ScopeId,
                domainEvent.OccurredAtUtc,
                domainEvent.ExecutionId,
                DataRightsCaseType.StaffRights,
                domainEvent.CaseId,
                domainEvent.ApprovalRevision,
                WorkspacesDataRightsCoordinates.Owner,
                WorkspacesDataRightsCoordinates.StaffOnboardingRecordType,
                domainEvent.ApplicationId,
                domainEvent.SelectedRecordVersion,
                domainEvent.CurrentRecordVersion,
                WorkspacesDataRightsCoordinates
                    .StaffOnboardingCorrectionFieldPolicyKey,
                WorkspaceStaffOnboardingCorrectionReceipt
                    .CurrentContractVersion,
                domainEvent.ReceiptId,
                domainEvent.ChangedFields
                    .Select(field => field.ToFieldKey())
                    .ToArray()),
            cancellationToken);
    }
}

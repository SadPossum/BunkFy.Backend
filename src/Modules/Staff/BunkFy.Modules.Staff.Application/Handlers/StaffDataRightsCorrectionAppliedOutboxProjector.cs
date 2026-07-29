namespace BunkFy.Modules.Staff.Application.Handlers;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Staff.Application.Mapping;
using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Staff.Domain.DataRights;
using BunkFy.Modules.Staff.Domain.Events;
using Gma.Framework.Application.Events;
using Gma.Framework.Messaging;

internal sealed class StaffDataRightsCorrectionAppliedOutboxProjector(
    IOutboxWriterRegistry outboxWriters)
    : IDomainEventHandler<StaffDataRightsCorrectionAppliedDomainEvent>
{
    public Task HandleAsync(
        StaffDataRightsCorrectionAppliedDomainEvent domainEvent,
        CancellationToken cancellationToken)
    {
        IOutboxWriter outbox =
            outboxWriters.GetRequired(StaffModuleMetadata.Name);
        return outbox.EnqueueAsync(
            new DataRightsTenantCorrectionAppliedIntegrationEvent(
                domainEvent.EventId,
                domainEvent.ScopeId,
                domainEvent.OccurredAtUtc,
                domainEvent.ExecutionId,
                DataRightsCaseType.StaffRights,
                domainEvent.CaseId,
                domainEvent.ApprovalRevision,
                StaffDataRightsCoordinates.Owner,
                StaffDataRightsCoordinates.StaffMemberRecordType,
                domainEvent.StaffMemberId,
                domainEvent.SelectedRecordVersion,
                domainEvent.CurrentRecordVersion,
                StaffDataRightsCoordinates.CorrectionFieldPolicyKey,
                StaffDataRightsCorrectionReceipt.CurrentContractVersion,
                domainEvent.ReceiptId,
                domainEvent.ChangedFields.Select(field => field.ToFieldKey()).ToArray()),
            cancellationToken);
    }
}

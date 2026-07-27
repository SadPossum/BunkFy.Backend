namespace BunkFy.Modules.Reservations.Application.Handlers;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Reservations.Application.Mapping;
using BunkFy.Modules.Reservations.Contracts;
using BunkFy.Modules.Reservations.Domain.DataRights;
using BunkFy.Modules.Reservations.Domain.Events;
using Gma.Framework.Application.Events;
using Gma.Framework.Messaging;

internal sealed class ReservationDataRightsCorrectionAppliedOutboxProjector(
    IOutboxWriterRegistry outboxWriters)
    : IDomainEventHandler<ReservationDataRightsCorrectionAppliedDomainEvent>
{
    public async Task HandleAsync(
        ReservationDataRightsCorrectionAppliedDomainEvent domainEvent,
        CancellationToken cancellationToken)
    {
        IOutboxWriter outbox = outboxWriters.GetRequired(ReservationsModuleMetadata.Name);
        await outbox.EnqueueAsync(
            new ReservationDataRightsCorrectionAppliedIntegrationEvent(
                domainEvent.EventId,
                domainEvent.ScopeId,
                domainEvent.OccurredAtUtc,
                domainEvent.ReceiptId,
                domainEvent.PropertyId,
                domainEvent.CaseId,
                domainEvent.ApprovalRevision,
                domainEvent.ReservationId,
                domainEvent.PreviousRecordVersion,
                domainEvent.CurrentRecordVersion,
                domainEvent.PreviousDetailsRevision,
                domainEvent.CurrentDetailsRevision,
                domainEvent.ChangedFields.Select(field => field.ToFieldKey()).ToArray(),
                domainEvent.DetailsChangeEventId),
            cancellationToken).ConfigureAwait(false);
        await outbox.EnqueueAsync(
            new DataRightsCorrectionAppliedIntegrationEvent(
                domainEvent.CompletionEventId,
                domainEvent.ScopeId,
                domainEvent.OccurredAtUtc,
                domainEvent.ExecutionId,
                domainEvent.PropertyId,
                domainEvent.CaseId,
                domainEvent.ApprovalRevision,
                ReservationsDataRightsCoordinates.Owner,
                ReservationsDataRightsCoordinates.ReservationRecordType,
                domainEvent.ReservationId,
                domainEvent.PreviousRecordVersion,
                domainEvent.CurrentRecordVersion,
                ReservationsDataRightsCoordinates.CorrectionFieldPolicyKey,
                ReservationDataRightsCorrectionReceipt.CurrentContractVersion,
                domainEvent.ReceiptId,
                domainEvent.ChangedFields.Select(field => field.ToFieldKey()).ToArray()),
            cancellationToken).ConfigureAwait(false);
    }
}

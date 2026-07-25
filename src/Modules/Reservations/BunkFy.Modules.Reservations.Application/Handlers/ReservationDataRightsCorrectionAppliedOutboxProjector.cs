namespace BunkFy.Modules.Reservations.Application.Handlers;

using BunkFy.Modules.Reservations.Application.Mapping;
using BunkFy.Modules.Reservations.Contracts;
using BunkFy.Modules.Reservations.Domain.Events;
using Gma.Framework.Application.Events;
using Gma.Framework.Messaging;

internal sealed class ReservationDataRightsCorrectionAppliedOutboxProjector(
    IOutboxWriterRegistry outboxWriters)
    : IDomainEventHandler<ReservationDataRightsCorrectionAppliedDomainEvent>
{
    public Task HandleAsync(
        ReservationDataRightsCorrectionAppliedDomainEvent domainEvent,
        CancellationToken cancellationToken)
    {
        IOutboxWriter outbox = outboxWriters.GetRequired(ReservationsModuleMetadata.Name);
        return outbox.EnqueueAsync(
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
            cancellationToken);
    }
}

namespace BunkFy.Modules.Ingestion.Application.Reservations;

using Gma.Framework.Messaging;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using BunkFy.Modules.Ingestion.Application.Commands;
using BunkFy.Modules.Ingestion.Contracts;
using BunkFy.Modules.Ingestion.Domain.Receipts;
using BunkFy.Modules.Ingestion.Domain.Reservations;
using global::BunkFy.Modules.Reservations.Contracts;

internal sealed class ReservationExternalRequestPublisher(
    IOutboxWriterRegistry outboxWriters,
    ISystemClock clock,
    IIdGenerator idGenerator)
{
    public async Task PublishAsync(
        ObservationReceipt receipt,
        ReservationSourceLink link,
        Guid operationId,
        ReservationDispatchKind kind,
        long? expectedRevision,
        NormalizedReservationObservation observation,
        CancellationToken cancellationToken)
    {
        IOutboxWriter outbox = outboxWriters.GetRequired(IngestionModuleMetadata.Name);
        if (kind == ReservationDispatchKind.Create)
        {
            await outbox.EnqueueAsync(new ExternalReservationCreateRequestedIntegrationEvent(
                idGenerator.NewId(), receipt.ScopeId, clock.UtcNow, operationId, receipt.Id, receipt.ConnectionId,
                receipt.PropertyId, link.SourceSystem, receipt.ExternalId, observation.Arrival!.Value,
                observation.Departure!.Value, observation.InventoryUnitIds, observation.PrimaryGuestName!,
                observation.Email, observation.Phone, observation.GuestCount!.Value, observation.Notes,
                observation.ExpectedArrivalTime, observation.ExpectedDepartureTime), cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        Guid reservationId = link.ReservationId
            ?? throw new InvalidOperationException("A non-create reservation dispatch requires a linked reservation.");
        long requiredExpectedRevision = expectedRevision
            ?? throw new InvalidOperationException("A non-create reservation dispatch requires an expected details revision.");

        if (kind == ReservationDispatchKind.Cancel)
        {
            await outbox.EnqueueAsync(new ExternalReservationCancellationRequestedIntegrationEvent(
                idGenerator.NewId(), receipt.ScopeId, clock.UtcNow, operationId, receipt.Id, receipt.ConnectionId,
                receipt.PropertyId, reservationId, link.SourceSystem, receipt.ExternalId,
                requiredExpectedRevision), cancellationToken).ConfigureAwait(false);
            return;
        }

        if (kind == ReservationDispatchKind.Amend)
        {
            await outbox.EnqueueAsync(new ExternalReservationAmendmentRequestedIntegrationEvent(
                idGenerator.NewId(), receipt.ScopeId, clock.UtcNow, operationId, receipt.Id, receipt.ConnectionId,
                receipt.PropertyId, reservationId, link.SourceSystem, receipt.ExternalId,
                requiredExpectedRevision, observation.Arrival!.Value, observation.Departure!.Value,
                observation.InventoryUnitIds, observation.PrimaryGuestName!, observation.Email, observation.Phone,
                observation.GuestCount!.Value, observation.Notes, observation.ExpectedArrivalTime,
                observation.ExpectedDepartureTime), cancellationToken).ConfigureAwait(false);
            return;
        }

        await outbox.EnqueueAsync(new ExternalReservationGuestDetailsChangeRequestedIntegrationEvent(
            idGenerator.NewId(), receipt.ScopeId, clock.UtcNow, operationId, receipt.Id, receipt.ConnectionId,
            receipt.PropertyId, reservationId, link.SourceSystem, receipt.ExternalId,
            requiredExpectedRevision, observation.PrimaryGuestName!, observation.Email, observation.Phone,
            observation.GuestCount!.Value, observation.Notes, observation.ExpectedArrivalTime,
            observation.ExpectedDepartureTime), cancellationToken).ConfigureAwait(false);
    }
}

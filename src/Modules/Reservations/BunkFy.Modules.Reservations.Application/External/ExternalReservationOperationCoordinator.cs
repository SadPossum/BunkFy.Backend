namespace BunkFy.Modules.Reservations.Application.External;

using Gma.Framework.Messaging;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using BunkFy.Modules.Reservations.Application.Ports;
using BunkFy.Modules.Reservations.Contracts;

internal sealed class ExternalReservationOperationCoordinator(
    IReservationExternalOperationRepository operations,
    IOutboxWriterRegistry outboxWriters,
    ISystemClock clock,
    IIdGenerator idGenerator)
{
    public async Task<bool> ShouldProcessAsync(
        ExternalReservationOperationContext context,
        ExternalReservationOperationKind kind,
        string requestFingerprint,
        CancellationToken cancellationToken)
    {
        ReservationExternalOperationRecord? existing = await operations
            .GetAsync(context.OperationId, cancellationToken)
            .ConfigureAwait(false);
        if (existing is null)
        {
            return true;
        }

        if (existing.Kind == kind &&
            string.Equals(existing.RequestFingerprint, requestFingerprint, StringComparison.Ordinal))
        {
            await this.PublishAsync(existing, cancellationToken).ConfigureAwait(false);
            return false;
        }

        await this.PublishAsync(
            new ReservationExternalOperationRecord(
                context.OperationId,
                context.ScopeId,
                context.ReceiptId,
                context.ConnectionId,
                context.PropertyId,
                kind,
                requestFingerprint,
                ExternalReservationOperationOutcome.OperationConflict,
                ReservationId: null,
                DetailsRevision: null,
                ReservationVersion: null,
                "Reservations.ExternalOperationConflict",
                clock.UtcNow),
            cancellationToken).ConfigureAwait(false);
        return false;
    }

    public async Task CompleteAsync(
        ExternalReservationOperationContext context,
        ExternalReservationOperationKind kind,
        string requestFingerprint,
        ExternalReservationOperationOutcome outcome,
        Guid? reservationId,
        long? detailsRevision,
        long? reservationVersion,
        string? errorCode,
        CancellationToken cancellationToken)
    {
        ReservationExternalOperationRecord completed = new(
            context.OperationId,
            context.ScopeId,
            context.ReceiptId,
            context.ConnectionId,
            context.PropertyId,
            kind,
            requestFingerprint,
            outcome,
            reservationId,
            detailsRevision,
            reservationVersion,
            errorCode,
            clock.UtcNow);
        await operations.AddAsync(completed, cancellationToken).ConfigureAwait(false);
        await this.PublishAsync(completed, cancellationToken).ConfigureAwait(false);
    }

    public Task PublishConflictAsync(
        ExternalReservationOperationContext context,
        ExternalReservationOperationKind kind,
        string requestFingerprint,
        CancellationToken cancellationToken) => this.PublishAsync(
        new ReservationExternalOperationRecord(
            context.OperationId,
            context.ScopeId,
            context.ReceiptId,
            context.ConnectionId,
            context.PropertyId,
            kind,
            requestFingerprint,
            ExternalReservationOperationOutcome.OperationConflict,
            ReservationId: null,
            DetailsRevision: null,
            ReservationVersion: null,
            "Reservations.ExternalOperationConflict",
            clock.UtcNow),
        cancellationToken);

    private Task PublishAsync(
        ReservationExternalOperationRecord operation,
        CancellationToken cancellationToken) =>
        outboxWriters.GetRequired(ReservationsModuleMetadata.Name).EnqueueAsync(
            new ExternalReservationOperationCompletedIntegrationEvent(
                idGenerator.NewId(),
                operation.ScopeId,
                clock.UtcNow,
                operation.OperationId,
                operation.ReceiptId,
                operation.ConnectionId,
                operation.PropertyId,
                operation.Kind,
                operation.Outcome,
                operation.ReservationId,
                operation.DetailsRevision,
                operation.ReservationVersion,
                operation.ErrorCode),
            cancellationToken);
}

internal sealed record ExternalReservationOperationContext(
    Guid OperationId,
    string ScopeId,
    Guid ReceiptId,
    Guid ConnectionId,
    Guid PropertyId);

namespace BunkFy.Modules.Reservations.Contracts;

using Gma.Framework.Messaging;
using Gma.Framework.Tenancy;
using Gma.Framework.Tenancy.Messaging;

[IntegrationEventName(EventType)]
[IntegrationEventVersion(EventVersion)]
[TenantScoped]
public sealed record ExternalReservationOperationCompletedIntegrationEvent : TenantIntegrationEvent
{
    public const string EventType = "external-reservation-operation-completed";
    public const int EventVersion = 1;

    public ExternalReservationOperationCompletedIntegrationEvent(
        Guid eventId,
        string tenantId,
        DateTimeOffset occurredAtUtc,
        Guid operationId,
        Guid receiptId,
        Guid connectionId,
        Guid propertyId,
        ExternalReservationOperationKind operationKind,
        ExternalReservationOperationOutcome outcome,
        Guid? reservationId,
        long? detailsRevision,
        long? reservationVersion,
        string? errorCode)
        : base(eventId, tenantId, occurredAtUtc, EventType, EventVersion)
    {
        ExternalReservationContractGuards.Common(operationId, receiptId, connectionId, propertyId);
        if (operationKind == ExternalReservationOperationKind.Unknown || !Enum.IsDefined(operationKind))
        {
            throw new ArgumentOutOfRangeException(nameof(operationKind));
        }

        if (outcome == ExternalReservationOperationOutcome.Unknown || !Enum.IsDefined(outcome))
        {
            throw new ArgumentOutOfRangeException(nameof(outcome));
        }

        if (reservationId == Guid.Empty || detailsRevision <= 0 || reservationVersion <= 0)
        {
            throw new ArgumentException("Optional reservation outcome values must be valid when present.");
        }

        this.OperationId = operationId;
        this.ReceiptId = receiptId;
        this.ConnectionId = connectionId;
        this.PropertyId = propertyId;
        this.OperationKind = operationKind;
        this.Outcome = outcome;
        this.ReservationId = reservationId;
        this.DetailsRevision = detailsRevision;
        this.ReservationVersion = reservationVersion;
        this.ErrorCode = ExternalReservationContractGuards.Optional(errorCode, 200, nameof(errorCode));
    }

    public Guid OperationId { get; }
    public Guid ReceiptId { get; }
    public Guid ConnectionId { get; }
    public Guid PropertyId { get; }
    public ExternalReservationOperationKind OperationKind { get; }
    public ExternalReservationOperationOutcome Outcome { get; }
    public Guid? ReservationId { get; }
    public long? DetailsRevision { get; }
    public long? ReservationVersion { get; }
    public string? ErrorCode { get; }
}

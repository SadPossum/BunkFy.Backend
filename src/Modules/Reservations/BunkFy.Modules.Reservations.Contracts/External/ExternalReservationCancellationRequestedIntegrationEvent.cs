namespace BunkFy.Modules.Reservations.Contracts;

using Gma.Framework.Messaging;
using Gma.Framework.Tenancy;
using Gma.Framework.Tenancy.Messaging;

[IntegrationEventName(EventType)]
[IntegrationEventVersion(EventVersion)]
[TenantScoped]
public sealed record ExternalReservationCancellationRequestedIntegrationEvent : TenantIntegrationEvent
{
    public const string EventType = "external-reservation-cancellation-requested";
    public const int EventVersion = 1;

    public ExternalReservationCancellationRequestedIntegrationEvent(
        Guid eventId,
        string tenantId,
        DateTimeOffset occurredAtUtc,
        Guid operationId,
        Guid receiptId,
        Guid connectionId,
        Guid propertyId,
        Guid reservationId,
        string sourceSystem,
        string sourceReference,
        long expectedDetailsRevision)
        : base(eventId, tenantId, occurredAtUtc, EventType, EventVersion)
    {
        ExternalReservationContractGuards.Common(operationId, receiptId, connectionId, propertyId);
        this.OperationId = operationId;
        this.ReceiptId = receiptId;
        this.ConnectionId = connectionId;
        this.PropertyId = propertyId;
        this.ReservationId = ExternalReservationContractGuards.Id(reservationId, nameof(reservationId));
        this.SourceSystem = ExternalReservationContractGuards.Required(sourceSystem, ReservationsContractLimits.SourceSystemMaxLength, nameof(sourceSystem)).ToLowerInvariant();
        this.SourceReference = ExternalReservationContractGuards.Required(sourceReference, ReservationsContractLimits.SourceReferenceMaxLength, nameof(sourceReference));
        this.ExpectedDetailsRevision = expectedDetailsRevision > 0
            ? expectedDetailsRevision
            : throw new ArgumentOutOfRangeException(nameof(expectedDetailsRevision));
    }

    public Guid OperationId { get; }
    public Guid ReceiptId { get; }
    public Guid ConnectionId { get; }
    public Guid PropertyId { get; }
    public Guid ReservationId { get; }
    public string SourceSystem { get; }
    public string SourceReference { get; }
    public long ExpectedDetailsRevision { get; }
}

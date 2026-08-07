namespace BunkFy.Modules.Reservations.Contracts;

using Gma.Framework.Messaging;
using Gma.Framework.Tenancy;
using Gma.Framework.Tenancy.Messaging;

[IntegrationEventName(EventType)]
[IntegrationEventVersion(EventVersion)]
[TenantScoped]
public sealed record ReservationGuestRecordLinkReadyIntegrationEvent :
    TenantIntegrationEvent
{
    public const string EventType = "reservation-guest-record-link-ready";
    public const int EventVersion = 1;

    public ReservationGuestRecordLinkReadyIntegrationEvent(
        Guid eventId,
        string tenantId,
        DateTimeOffset occurredAtUtc,
        Guid operationId,
        Guid propertyId,
        Guid reservationId,
        int dispatchRevision)
        : base(eventId, tenantId, occurredAtUtc, EventType, EventVersion)
    {
        this.OperationId = IntegrationEventContractGuards.RequireId(
            operationId,
            nameof(operationId));
        this.PropertyId = IntegrationEventContractGuards.RequireId(
            propertyId,
            nameof(propertyId));
        this.ReservationId = IntegrationEventContractGuards.RequireId(
            reservationId,
            nameof(reservationId));
        this.DispatchRevision = dispatchRevision > 0
            ? dispatchRevision
            : throw new ArgumentOutOfRangeException(nameof(dispatchRevision));
    }

    public Guid OperationId { get; }
    public Guid PropertyId { get; }
    public Guid ReservationId { get; }
    public int DispatchRevision { get; }
}

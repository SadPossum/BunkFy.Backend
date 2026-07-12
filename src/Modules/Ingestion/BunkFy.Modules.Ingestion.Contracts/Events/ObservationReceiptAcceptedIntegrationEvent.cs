namespace BunkFy.Modules.Ingestion.Contracts;

using Gma.Framework.Messaging;
using Gma.Framework.Tenancy;
using Gma.Framework.Tenancy.Messaging;

[IntegrationEventName(EventType)]
[IntegrationEventVersion(EventVersion)]
[TenantScoped]
public sealed record ObservationReceiptAcceptedIntegrationEvent : TenantIntegrationEvent
{
    public const string EventType = "observation-receipt-accepted";
    public const int EventVersion = 1;

    public ObservationReceiptAcceptedIntegrationEvent(
        Guid eventId,
        string tenantId,
        DateTimeOffset occurredAtUtc,
        Guid receiptId,
        Guid connectionId,
        Guid propertyId)
        : base(eventId, tenantId, occurredAtUtc, EventType, EventVersion)
    {
        this.ReceiptId = Require(receiptId, nameof(receiptId));
        this.ConnectionId = Require(connectionId, nameof(connectionId));
        this.PropertyId = Require(propertyId, nameof(propertyId));
    }

    public Guid ReceiptId { get; }
    public Guid ConnectionId { get; }
    public Guid PropertyId { get; }

    private static Guid Require(Guid value, string parameterName) =>
        value != Guid.Empty ? value : throw new ArgumentException("A non-empty id is required.", parameterName);
}

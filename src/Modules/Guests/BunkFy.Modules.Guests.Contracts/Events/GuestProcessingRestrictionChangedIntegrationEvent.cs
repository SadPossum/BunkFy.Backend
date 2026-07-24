namespace BunkFy.Modules.Guests.Contracts;

using Gma.Framework.Messaging;
using Gma.Framework.Tenancy;
using Gma.Framework.Tenancy.Messaging;

[IntegrationEventName(EventType)]
[IntegrationEventVersion(EventVersion)]
[TenantScoped]
public sealed record GuestProcessingRestrictionChangedIntegrationEvent : TenantIntegrationEvent
{
    public const string EventType = "guest-processing-restriction-changed";
    public const int EventVersion = 1;

    public GuestProcessingRestrictionChangedIntegrationEvent(
        Guid eventId,
        string tenantId,
        DateTimeOffset occurredAtUtc,
        Guid propertyId,
        Guid guestId,
        int contractVersion,
        long projectionRevision,
        bool isRestricted)
        : base(eventId, tenantId, occurredAtUtc, EventType, EventVersion)
    {
        this.PropertyId = IntegrationEventContractGuards.RequireId(propertyId, nameof(propertyId));
        this.GuestId = IntegrationEventContractGuards.RequireId(guestId, nameof(guestId));
        this.ContractVersion = contractVersion > 0
            ? contractVersion
            : throw new ArgumentOutOfRangeException(nameof(contractVersion));
        this.ProjectionRevision = projectionRevision > 0
            ? projectionRevision
            : throw new ArgumentOutOfRangeException(nameof(projectionRevision));
        this.IsRestricted = isRestricted;
    }

    public Guid PropertyId { get; }
    public Guid GuestId { get; }
    public int ContractVersion { get; }
    public long ProjectionRevision { get; }
    public bool IsRestricted { get; }
}

namespace BunkFy.Modules.Reservations.Contracts;

using Gma.Framework.Messaging;
using Gma.Framework.Tenancy;
using Gma.Framework.Tenancy.Messaging;

[IntegrationEventName(EventType)]
[IntegrationEventVersion(EventVersion)]
[TenantScoped]
public sealed record ExternalReservationGuestDetailsChangeRequestedIntegrationEvent : TenantIntegrationEvent
{
    public const string EventType = "external-reservation-guest-details-change-requested";
    public const int EventVersion = 1;

    public ExternalReservationGuestDetailsChangeRequestedIntegrationEvent(
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
        long expectedDetailsRevision,
        string primaryGuestName,
        string? email,
        string? phone,
        int guestCount,
        string? notes)
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
        this.PrimaryGuestName = ExternalReservationContractGuards.Required(primaryGuestName, ReservationsContractLimits.PrimaryGuestNameMaxLength, nameof(primaryGuestName));
        this.Email = ExternalReservationContractGuards.Optional(email, ReservationsContractLimits.EmailMaxLength, nameof(email));
        this.Phone = ExternalReservationContractGuards.Optional(phone, ReservationsContractLimits.PhoneMaxLength, nameof(phone));
        this.GuestCount = guestCount > 0 ? guestCount : throw new ArgumentOutOfRangeException(nameof(guestCount));
        this.Notes = ExternalReservationContractGuards.Optional(notes, ReservationsContractLimits.NotesMaxLength, nameof(notes));
    }

    public Guid OperationId { get; }
    public Guid ReceiptId { get; }
    public Guid ConnectionId { get; }
    public Guid PropertyId { get; }
    public Guid ReservationId { get; }
    public string SourceSystem { get; }
    public string SourceReference { get; }
    public long ExpectedDetailsRevision { get; }
    public string PrimaryGuestName { get; }
    public string? Email { get; }
    public string? Phone { get; }
    public int GuestCount { get; }
    public string? Notes { get; }
}

namespace BunkFy.Modules.Reservations.Domain.Events;

using Gma.Framework.Domain;
using BunkFy.Modules.Reservations.Domain.Aggregates;

public sealed record ReservationDetailsChangedDomainEvent : ScopedDomainEvent
{
    public ReservationDetailsChangedDomainEvent(
        Guid eventId,
        DateTimeOffset occurredAtUtc,
        string scopeId,
        Guid reservationId,
        Guid propertyId,
        long fromRevision,
        long toRevision,
        ReservationDetailsChangeOrigin origin,
        string? actorId,
        Guid? adapterConnectionId,
        Guid? externalOperationId,
        Guid correlationId,
        IReadOnlyCollection<string> changedFields,
        ReservationDetailsSnapshot? before,
        ReservationDetailsSnapshot after)
        : base(eventId, occurredAtUtc, scopeId)
    {
        this.ReservationId = DomainEventGuards.RequireId(reservationId, nameof(reservationId));
        this.PropertyId = DomainEventGuards.RequireId(propertyId, nameof(propertyId));
        if (fromRevision < 0 || toRevision != fromRevision + 1)
        {
            throw new ArgumentOutOfRangeException(nameof(toRevision));
        }

        if (origin == ReservationDetailsChangeOrigin.Unknown || !Enum.IsDefined(origin))
        {
            throw new ArgumentOutOfRangeException(nameof(origin));
        }

        this.FromRevision = fromRevision;
        this.ToRevision = toRevision;
        this.Origin = origin;
        this.ActorId = string.IsNullOrWhiteSpace(actorId) ? null : actorId.Trim();
        this.AdapterConnectionId = adapterConnectionId;
        this.ExternalOperationId = externalOperationId;
        this.CorrelationId = DomainEventGuards.RequireId(correlationId, nameof(correlationId));
        ArgumentNullException.ThrowIfNull(changedFields);
        string[] fields = changedFields.Distinct(StringComparer.Ordinal).ToArray();
        if (fields.Length == 0 || fields.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("At least one changed field is required.", nameof(changedFields));
        }

        this.ChangedFields = Array.AsReadOnly(fields);
        this.Before = before;
        this.After = after ?? throw new ArgumentNullException(nameof(after));
    }

    public Guid ReservationId { get; }
    public Guid PropertyId { get; }
    public long FromRevision { get; }
    public long ToRevision { get; }
    public ReservationDetailsChangeOrigin Origin { get; }
    public string? ActorId { get; }
    public Guid? AdapterConnectionId { get; }
    public Guid? ExternalOperationId { get; }
    public Guid CorrelationId { get; }
    public IReadOnlyCollection<string> ChangedFields { get; }
    public ReservationDetailsSnapshot? Before { get; }
    public ReservationDetailsSnapshot After { get; }
}

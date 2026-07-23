namespace BunkFy.Modules.Properties.Domain.Events;

using BunkFy.Modules.Properties.Domain.Aggregates;
using BunkFy.Modules.Properties.Domain.ValueObjects;
using Gma.Framework.Domain;

public sealed record PropertyProcessingSuspendedDomainEvent : ScopedDomainEvent
{
    public PropertyProcessingSuspendedDomainEvent(
        Guid eventId,
        DateTimeOffset occurredAtUtc,
        Guid propertyId,
        string tenantId,
        PropertyGovernanceBinding binding,
        IReadOnlyCollection<PropertyGovernanceAcknowledgement> acknowledgements,
        string actorId,
        long propertyVersion)
        : base(eventId, occurredAtUtc, tenantId)
    {
        this.PropertyId = DomainEventGuards.RequireId(propertyId, nameof(propertyId));
        this.Binding = binding ?? throw new ArgumentNullException(nameof(binding));
        this.Acknowledgements = acknowledgements?.ToArray() ?? throw new ArgumentNullException(nameof(acknowledgements));
        this.ActorId = DomainEventGuards.NormalizeRequiredText(actorId, Property.ActorIdMaxLength, nameof(actorId));
        this.PropertyVersion = propertyVersion > 0
            ? propertyVersion
            : throw new ArgumentOutOfRangeException(nameof(propertyVersion));
    }

    public Guid PropertyId { get; }
    public PropertyGovernanceBinding Binding { get; }
    public IReadOnlyCollection<PropertyGovernanceAcknowledgement> Acknowledgements { get; }
    public string ActorId { get; }
    public long PropertyVersion { get; }
}

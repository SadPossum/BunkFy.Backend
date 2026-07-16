namespace BunkFy.Modules.Inventory.Domain.Events;

using Gma.Framework.Domain;

public sealed record RoomRetirementFinalizationRequestedDomainEvent(
    Guid EventId,
    DateTimeOffset OccurredAtUtc,
    string ScopeId,
    Guid TopologyChangeId,
    Guid PropertyId,
    Guid RoomId)
    : DomainEvent(EventId, OccurredAtUtc);

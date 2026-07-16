namespace BunkFy.Modules.Inventory.Domain.Events;

using Gma.Framework.Domain;

public sealed record BedRetirementFinalizationRequestedDomainEvent(
    Guid EventId,
    DateTimeOffset OccurredAtUtc,
    string ScopeId,
    Guid TopologyChangeId,
    Guid PropertyId,
    Guid RoomId,
    Guid BedId)
    : DomainEvent(EventId, OccurredAtUtc);

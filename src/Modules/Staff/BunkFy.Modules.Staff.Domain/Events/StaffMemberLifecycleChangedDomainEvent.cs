namespace BunkFy.Modules.Staff.Domain.Events;

using Gma.Framework.Domain;
using BunkFy.Modules.Staff.Domain.Aggregates;

public sealed record StaffMemberLifecycleChangedDomainEvent(
    Guid EventId,
    DateTimeOffset OccurredAtUtc,
    string ScopeId,
    Guid StaffMemberId,
    StaffMemberState Status,
    DateOnly EffectiveOn,
    long StaffVersion,
    string? ActorId = null) : ScopedDomainEvent(EventId, OccurredAtUtc, ScopeId);

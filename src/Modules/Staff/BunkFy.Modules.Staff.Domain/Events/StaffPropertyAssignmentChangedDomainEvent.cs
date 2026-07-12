namespace BunkFy.Modules.Staff.Domain.Events;

using Gma.Framework.Domain;
using BunkFy.Modules.Staff.Domain.Aggregates;

public sealed record StaffPropertyAssignmentChangedDomainEvent(
    Guid EventId,
    DateTimeOffset OccurredAtUtc,
    string ScopeId,
    Guid StaffMemberId,
    Guid AssignmentId,
    Guid PropertyId,
    bool IsCurrent,
    bool IsPrimary,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    long StaffVersion) : ScopedDomainEvent(EventId, OccurredAtUtc, ScopeId);

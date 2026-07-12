namespace BunkFy.Modules.Staff.Domain.Events;

using Gma.Framework.Domain;
using BunkFy.Modules.Staff.Domain.Aggregates;

public sealed record StaffAuthSubjectChangedDomainEvent(
    Guid EventId,
    DateTimeOffset OccurredAtUtc,
    string ScopeId,
    Guid StaffMemberId,
    string? AuthSubjectId,
    long StaffVersion) : ScopedDomainEvent(EventId, OccurredAtUtc, ScopeId);

namespace BunkFy.Modules.Staff.Domain.Events;

using Gma.Framework.Domain;
using BunkFy.Modules.Staff.Domain.Aggregates;

public sealed record StaffMemberCreatedDomainEvent(
    Guid EventId,
    DateTimeOffset OccurredAtUtc,
    string ScopeId,
    Guid StaffMemberId,
    StaffMemberState Status,
    string? AuthSubjectId,
    long StaffVersion) : ScopedDomainEvent(EventId, OccurredAtUtc, ScopeId);

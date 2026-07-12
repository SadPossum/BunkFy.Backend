namespace BunkFy.Modules.Reservations.Domain.Events;

using Gma.Framework.Domain;
using BunkFy.Modules.Reservations.Domain.Aggregates;

public sealed record ReservationGuestStayChangedDomainEvent(
    Guid EventId,
    DateTimeOffset OccurredAtUtc,
    string ScopeId,
    Guid ReservationId,
    Guid PropertyId,
    Guid GuestId,
    ReservationGuestRole Role,
    DateOnly Arrival,
    DateOnly Departure,
    ReservationState Status,
    DateOnly? CheckedInBusinessDate,
    DateOnly? NoShowBusinessDate,
    DateOnly? CheckedOutBusinessDate,
    bool IsCurrentParticipant,
    long ReservationVersion)
    : ScopedDomainEvent(EventId, OccurredAtUtc, ScopeId);

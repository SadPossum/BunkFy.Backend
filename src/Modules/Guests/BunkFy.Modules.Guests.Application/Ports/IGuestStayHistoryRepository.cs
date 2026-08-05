namespace BunkFy.Modules.Guests.Application.Ports;

using BunkFy.Modules.Guests.Contracts;
using Gma.Framework.Pagination;

public interface IGuestStayHistoryRepository
{
    Task ApplyAsync(GuestStayHistoryWriteModel stay, CancellationToken cancellationToken);
    Task<GuestStayHistoryListResponse> ListAsync(
        Guid propertyId,
        Guid guestId,
        PageRequest pageRequest,
        CancellationToken cancellationToken);
}

public sealed record GuestStayHistoryWriteModel(
    string ScopeId,
    Guid GuestId,
    Guid ReservationId,
    Guid PropertyId,
    GuestStayRole Role,
    DateOnly Arrival,
    DateOnly Departure,
    GuestStayStatus Status,
    DateOnly? CheckedInBusinessDate,
    DateOnly? NoShowBusinessDate,
    DateOnly? CheckedOutBusinessDate,
    bool IsCurrentParticipant,
    long ReservationVersion,
    int ProjectionContractVersion,
    DateTimeOffset ObservedAtUtc);

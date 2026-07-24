namespace BunkFy.Modules.Guests.Application.Ports;

using BunkFy.Modules.Guests.Contracts;
using BunkFy.Modules.Guests.Domain.Aggregates;
using Gma.Framework.Pagination;

public interface IGuestProfileRepository
{
    Task AddAsync(GuestProfile profile, CancellationToken cancellationToken);
    Task<GuestProfile?> GetVisibleAsync(Guid propertyId, Guid guestId, CancellationToken cancellationToken);
    Task<GuestProfile?> GetForDataRightsAsync(
        Guid propertyId,
        Guid guestId,
        CancellationToken cancellationToken);
    Task<GuestListResponse> ListVisibleAsync(
        Guid propertyId,
        string? search,
        GuestStatus? status,
        PageRequest pageRequest,
        CancellationToken cancellationToken);
}

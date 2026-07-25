namespace BunkFy.Modules.Guests.Application.Ports;

using BunkFy.Modules.Guests.Contracts;
using BunkFy.Modules.Guests.Domain.DataRights;
using Gma.Framework.Pagination;

public interface IGuestDataHoldRepository
{
    Task<GuestDataHoldReceipt?> FindReceiptByIdempotencyKeyAsync(
        Guid idempotencyKey,
        CancellationToken cancellationToken);

    Task<GuestDataHold?> GetAsync(
        Guid propertyId,
        Guid guestId,
        Guid holdId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<GuestDataHold>> ListAsync(
        Guid propertyId,
        Guid guestId,
        GuestDataHoldStatus? status,
        PageRequest pageRequest,
        CancellationToken cancellationToken);

    Task AddAsync(GuestDataHold hold, CancellationToken cancellationToken);

    Task AddReceiptAsync(
        GuestDataHoldReceipt receipt,
        CancellationToken cancellationToken);
}

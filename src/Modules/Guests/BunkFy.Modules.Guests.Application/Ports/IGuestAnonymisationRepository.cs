namespace BunkFy.Modules.Guests.Application.Ports;

using BunkFy.Modules.Guests.Domain.DataRights;

public interface IGuestAnonymisationRepository
{
    Task<GuestAnonymisationReceipt?> FindReceiptByIdempotencyKeyAsync(
        Guid idempotencyKey,
        CancellationToken cancellationToken);

    Task<GuestAnonymisationTombstone?> GetTombstoneAsync(
        Guid guestId,
        CancellationToken cancellationToken);

    Task AddAsync(
        GuestAnonymisationReceipt receipt,
        GuestAnonymisationTombstone tombstone,
        CancellationToken cancellationToken);
}

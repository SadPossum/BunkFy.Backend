namespace BunkFy.Modules.Guests.Application.Ports;

using BunkFy.Modules.Guests.Domain.Aggregates;
using BunkFy.Modules.Guests.Domain.DataRights;

public interface IGuestAnonymisationRestoreRepository
{
    Task<GuestProfile?> GetProfileAsync(
        Guid guestId,
        CancellationToken cancellationToken);

    Task<GuestAnonymisationTombstone?> GetTombstoneAsync(
        Guid guestId,
        CancellationToken cancellationToken);

    Task<GuestAnonymisationRestoreReceipt?> GetReceiptAsync(
        Guid ledgerEntryId,
        CancellationToken cancellationToken);

    Task AddAsync(
        GuestAnonymisationRestoreReceipt receipt,
        GuestAnonymisationTombstone? newTombstone,
        GuestProfile? newProfile,
        CancellationToken cancellationToken);
}

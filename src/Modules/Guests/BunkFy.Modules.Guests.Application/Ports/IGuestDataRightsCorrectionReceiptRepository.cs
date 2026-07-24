namespace BunkFy.Modules.Guests.Application.Ports;

using BunkFy.Modules.Guests.Domain.DataRights;

public interface IGuestDataRightsCorrectionReceiptRepository
{
    Task<GuestDataRightsCorrectionReceipt?> FindByIdempotencyKeyAsync(
        Guid idempotencyKey,
        CancellationToken cancellationToken);

    Task AddAsync(
        GuestDataRightsCorrectionReceipt receipt,
        CancellationToken cancellationToken);
}

namespace BunkFy.Modules.Guests.Persistence.Repositories;

using BunkFy.Modules.Guests.Application.Ports;
using BunkFy.Modules.Guests.Contracts;
using BunkFy.Modules.Guests.Domain.DataRights;
using BunkFy.Modules.Guests.Domain.Models;
using Gma.Framework.Pagination;
using Microsoft.EntityFrameworkCore;

internal sealed class GuestDataHoldRepository(GuestsDbContext dbContext)
    : IGuestDataHoldRepository
{
    public Task<GuestDataHoldReceipt?> FindReceiptByIdempotencyKeyAsync(
        Guid idempotencyKey,
        CancellationToken cancellationToken) =>
        dbContext.DataHoldReceipts.AsNoTracking().FirstOrDefaultAsync(
            receipt => receipt.IdempotencyKey == idempotencyKey,
            cancellationToken);

    public Task<GuestDataHold?> GetAsync(
        Guid propertyId,
        Guid guestId,
        Guid holdId,
        CancellationToken cancellationToken) =>
        dbContext.DataHolds.FirstOrDefaultAsync(
            hold =>
                hold.Id == holdId &&
                hold.PropertyId == propertyId &&
                hold.GuestId == guestId,
            cancellationToken);

    public async Task<IReadOnlyCollection<GuestDataHold>> ListAsync(
        Guid propertyId,
        Guid guestId,
        GuestDataHoldStatus? status,
        PageRequest pageRequest,
        CancellationToken cancellationToken)
    {
        IQueryable<GuestDataHold> query = dbContext.DataHolds.AsNoTracking()
            .Where(hold => hold.PropertyId == propertyId && hold.GuestId == guestId);
        if (status.HasValue)
        {
            GuestDataHoldState state = status.Value switch
            {
                GuestDataHoldStatus.Active => GuestDataHoldState.Active,
                GuestDataHoldStatus.Released => GuestDataHoldState.Released,
                _ => GuestDataHoldState.Unknown
            };
            query = query.Where(hold => hold.State == state);
        }

        return await query
            .OrderByDescending(hold => hold.PlacedAtUtc)
            .ThenBy(hold => hold.Id)
            .Skip(pageRequest.SkipCount)
            .Take(pageRequest.PageSize)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public Task AddAsync(GuestDataHold hold, CancellationToken cancellationToken)
    {
        dbContext.DataHolds.Add(hold);
        return Task.CompletedTask;
    }

    public Task AddReceiptAsync(
        GuestDataHoldReceipt receipt,
        CancellationToken cancellationToken)
    {
        dbContext.DataHoldReceipts.Add(receipt);
        return Task.CompletedTask;
    }
}

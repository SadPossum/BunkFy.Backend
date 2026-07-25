namespace BunkFy.Modules.Reservations.Persistence.Repositories;

using BunkFy.Modules.Reservations.Application.Ports;
using BunkFy.Modules.Reservations.Contracts;
using BunkFy.Modules.Reservations.Domain.DataRights;
using BunkFy.Modules.Reservations.Domain.Models;
using Gma.Framework.Pagination;
using Microsoft.EntityFrameworkCore;
using ContractDataHoldStatus = Contracts.ReservationDataHoldStatus;

internal sealed class ReservationDataHoldRepository(
    ReservationsDbContext dbContext)
    : IReservationDataHoldRepository
{
    public Task<ReservationDataHoldReceipt?>
        FindReceiptByIdempotencyKeyAsync(
            Guid idempotencyKey,
            CancellationToken cancellationToken) =>
        dbContext.DataHoldReceipts.FirstOrDefaultAsync(
            receipt => receipt.IdempotencyKey == idempotencyKey,
            cancellationToken);

    public Task<ReservationDataHold?> GetAsync(
        Guid propertyId,
        Guid reservationId,
        Guid holdId,
        CancellationToken cancellationToken) =>
        dbContext.DataHolds.FirstOrDefaultAsync(
            hold =>
                hold.Id == holdId &&
                hold.PropertyId == propertyId &&
                hold.ReservationId == reservationId,
            cancellationToken);

    public async Task<IReadOnlyCollection<ReservationDataHold>> ListAsync(
        Guid propertyId,
        Guid reservationId,
        ContractDataHoldStatus? status,
        PageRequest pageRequest,
        CancellationToken cancellationToken)
    {
        IQueryable<ReservationDataHold> query = dbContext.DataHolds
            .AsNoTracking()
            .Where(hold =>
                hold.PropertyId == propertyId &&
                hold.ReservationId == reservationId);
        if (status.HasValue)
        {
            ReservationDataHoldState state =
                status == ContractDataHoldStatus.Active
                    ? ReservationDataHoldState.Active
                    : ReservationDataHoldState.Released;
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

    public Task AddAsync(
        ReservationDataHold hold,
        CancellationToken cancellationToken)
    {
        dbContext.DataHolds.Add(hold);
        return Task.CompletedTask;
    }

    public Task AddReceiptAsync(
        ReservationDataHoldReceipt receipt,
        CancellationToken cancellationToken)
    {
        dbContext.DataHoldReceipts.Add(receipt);
        return Task.CompletedTask;
    }
}

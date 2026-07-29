namespace BunkFy.Modules.Staff.Persistence.Repositories;

using BunkFy.Modules.Staff.Application.Ports;
using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Staff.Domain.DataRights;
using BunkFy.Modules.Staff.Domain.Models;
using Gma.Framework.Pagination;
using Microsoft.EntityFrameworkCore;

internal sealed class StaffDataHoldRepository(StaffDbContext dbContext)
    : IStaffDataHoldRepository
{
    public Task<StaffDataHoldReceipt?>
        FindReceiptByIdempotencyKeyAsync(
            Guid idempotencyKey,
            CancellationToken cancellationToken) =>
        dbContext.Set<StaffDataHoldReceipt>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                receipt =>
                    receipt.IdempotencyKey == idempotencyKey,
                cancellationToken);

    public Task<StaffDataHold?> GetAsync(
        Guid staffMemberId,
        Guid holdId,
        CancellationToken cancellationToken) =>
        dbContext.Set<StaffDataHold>()
            .SingleOrDefaultAsync(
                hold =>
                    hold.StaffMemberId == staffMemberId &&
                    hold.Id == holdId,
                cancellationToken);

    public async Task<IReadOnlyCollection<StaffDataHold>> ListAsync(
        Guid staffMemberId,
        StaffDataHoldStatus? status,
        PageRequest pageRequest,
        CancellationToken cancellationToken)
    {
        IQueryable<StaffDataHold> query =
            ApplyStatus(
                dbContext.Set<StaffDataHold>()
                    .AsNoTracking()
                    .Where(hold =>
                        hold.StaffMemberId == staffMemberId),
                status);
        return await query
            .OrderByDescending(hold => hold.PlacedAtUtc)
            .ThenBy(hold => hold.Id)
            .Skip(pageRequest.SkipCount)
            .Take(pageRequest.PageSize)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public Task<long> CountAsync(
        Guid staffMemberId,
        StaffDataHoldStatus? status,
        CancellationToken cancellationToken) =>
        ApplyStatus(
                dbContext.Set<StaffDataHold>()
                    .AsNoTracking()
                    .Where(hold =>
                        hold.StaffMemberId == staffMemberId),
                status)
            .LongCountAsync(cancellationToken);

    public Task AddAsync(
        StaffDataHold hold,
        CancellationToken cancellationToken)
    {
        dbContext.Set<StaffDataHold>().Add(hold);
        return Task.CompletedTask;
    }

    public Task AddReceiptAsync(
        StaffDataHoldReceipt receipt,
        CancellationToken cancellationToken)
    {
        dbContext.Set<StaffDataHoldReceipt>().Add(receipt);
        return Task.CompletedTask;
    }

    private static IQueryable<StaffDataHold> ApplyStatus(
        IQueryable<StaffDataHold> query,
        StaffDataHoldStatus? status) =>
        status switch
        {
            StaffDataHoldStatus.Active =>
                query.Where(hold =>
                    hold.State == StaffDataHoldState.Active),
            StaffDataHoldStatus.Released =>
                query.Where(hold =>
                    hold.State == StaffDataHoldState.Released),
            _ => query
        };
}

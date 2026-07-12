namespace BunkFy.Modules.Ingestion.Persistence.Repositories;

using Gma.Framework.Pagination;
using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Contracts;
using BunkFy.Modules.Ingestion.Domain.LegalHolds;
using BunkFy.Modules.Ingestion.Domain.Receipts;
using Microsoft.EntityFrameworkCore;

internal sealed class LegalHoldRepository(IngestionDbContext dbContext)
    : ILegalHoldRepository, ILegalHoldReader
{
    public Task<bool> HasPurgingRawPayloadsAsync(
        Guid propertyId,
        CancellationToken cancellationToken) => dbContext.ObservationReceipts.AsNoTracking().AnyAsync(
        receipt => receipt.PropertyId == propertyId &&
                   receipt.RawPayloadRetentionState == RawPayloadRetentionState.Purging,
        cancellationToken);

    public Task<LegalHold?> GetAsync(
        Guid propertyId,
        Guid holdId,
        CancellationToken cancellationToken) => dbContext.LegalHolds.FirstOrDefaultAsync(
        legalHold => legalHold.PropertyId == propertyId && legalHold.Id == holdId,
        cancellationToken);

    public Task AddAsync(LegalHold legalHold, CancellationToken cancellationToken)
    {
        dbContext.LegalHolds.Add(legalHold);
        return Task.CompletedTask;
    }

    async Task<LegalHoldDto?> ILegalHoldReader.GetAsync(
        Guid propertyId,
        Guid holdId,
        CancellationToken cancellationToken) => await dbContext.LegalHolds.AsNoTracking()
        .Where(legalHold => legalHold.PropertyId == propertyId && legalHold.Id == holdId)
        .Select(legalHold => new LegalHoldDto(
            legalHold.Id,
            legalHold.PropertyId,
            legalHold.Reason,
            (LegalHoldStatus)(int)legalHold.State,
            legalHold.PlacedBy,
            legalHold.PlacedAtUtc,
            legalHold.ReleasedBy,
            legalHold.ReleaseReason,
            legalHold.ReleasedAtUtc,
            legalHold.Version))
        .FirstOrDefaultAsync(cancellationToken)
        .ConfigureAwait(false);

    public async Task<LegalHoldListResponse> ListAsync(
        Guid propertyId,
        LegalHoldStatus? status,
        PageRequest pageRequest,
        CancellationToken cancellationToken)
    {
        IQueryable<LegalHold> query = dbContext.LegalHolds.AsNoTracking()
            .Where(legalHold => legalHold.PropertyId == propertyId);
        if (status.HasValue)
        {
            LegalHoldState state = (LegalHoldState)(int)status.Value;
            query = query.Where(legalHold => legalHold.State == state);
        }

        long totalCount = await query.LongCountAsync(cancellationToken).ConfigureAwait(false);
        LegalHoldDto[] values = await query
            .OrderByDescending(legalHold => legalHold.PlacedAtUtc)
            .ThenBy(legalHold => legalHold.Id)
            .Skip(pageRequest.SkipCount)
            .Take(pageRequest.PageSize)
            .Select(legalHold => new LegalHoldDto(
                legalHold.Id,
                legalHold.PropertyId,
                legalHold.Reason,
                (LegalHoldStatus)(int)legalHold.State,
                legalHold.PlacedBy,
                legalHold.PlacedAtUtc,
                legalHold.ReleasedBy,
                legalHold.ReleaseReason,
                legalHold.ReleasedAtUtc,
                legalHold.Version))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        return new LegalHoldListResponse(
            values,
            pageRequest.Page,
            pageRequest.PageSize,
            totalCount);
    }
}

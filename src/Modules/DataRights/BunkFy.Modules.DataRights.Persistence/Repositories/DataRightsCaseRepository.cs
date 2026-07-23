namespace BunkFy.Modules.DataRights.Persistence.Repositories;

using BunkFy.Modules.DataRights.Application.Mapping;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Models;
using Gma.Framework.Pagination;
using Microsoft.EntityFrameworkCore;

internal sealed class DataRightsCaseRepository(DataRightsDbContext dbContext)
    : IDataRightsCaseRepository
{
    public Task AddAsync(DataRightsCase dataRightsCase, CancellationToken cancellationToken)
    {
        dbContext.Cases.Add(dataRightsCase);
        return Task.CompletedTask;
    }

    public Task<DataRightsCase?> GetAsync(
        Guid propertyId,
        Guid caseId,
        CancellationToken cancellationToken) => dbContext.Cases.FirstOrDefaultAsync(
        dataRightsCase =>
            dataRightsCase.Id == caseId &&
            dataRightsCase.PropertyId == propertyId,
        cancellationToken);

    public async Task<DataRightsCaseListResponse> ListAsync(
        Guid propertyId,
        DataRightsCaseStatus? status,
        PageRequest pageRequest,
        CancellationToken cancellationToken)
    {
        IQueryable<DataRightsCase> query = dbContext.Cases
            .AsNoTracking()
            .Where(dataRightsCase => dataRightsCase.PropertyId == propertyId);
        if (status.HasValue)
        {
            DataRightsCaseState state = (DataRightsCaseState)status.Value;
            query = query.Where(dataRightsCase => dataRightsCase.Status == state);
        }

        DataRightsCase[] rows = await query
            .OrderByDescending(dataRightsCase => dataRightsCase.CreatedAtUtc)
            .ThenBy(dataRightsCase => dataRightsCase.Id)
            .Skip(pageRequest.SkipCount)
            .Take(pageRequest.PageSize)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        return new(
            rows.Select(dataRightsCase => dataRightsCase.ToDto()).ToArray(),
            pageRequest.Page,
            pageRequest.PageSize);
    }
}

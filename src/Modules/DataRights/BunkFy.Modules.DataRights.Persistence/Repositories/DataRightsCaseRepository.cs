namespace BunkFy.Modules.DataRights.Persistence.Repositories;

using BunkFy.Modules.DataRights.Application.Mapping;
using BunkFy.Modules.DataRights.Application.Models;
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

    public async Task<DataRightsCase?> GetAsync(
        DataRightsCaseScope scope,
        Guid caseId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        return await ApplyScope(dbContext.Cases, scope)
            .FirstOrDefaultAsync(
                dataRightsCase => dataRightsCase.Id == caseId,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<DataRightsCaseListResponse> ListAsync(
        DataRightsCaseScope scope,
        DataRightsCaseStatus? status,
        PageRequest pageRequest,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        IQueryable<DataRightsCase> query = ApplyScope(
            dbContext.Cases.AsNoTracking(),
            scope);
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

    private static IQueryable<DataRightsCase> ApplyScope(
        IQueryable<DataRightsCase> query,
        DataRightsCaseScope scope) =>
        scope.IsTenant
            ? query.Where(dataRightsCase =>
                dataRightsCase.Kind == (DataRightsCaseKind)scope.CaseType &&
                dataRightsCase.PropertyId == null)
            : query.Where(dataRightsCase =>
                dataRightsCase.Kind == (DataRightsCaseKind)scope.CaseType &&
                dataRightsCase.PropertyId == scope.PropertyId!.Value);
}

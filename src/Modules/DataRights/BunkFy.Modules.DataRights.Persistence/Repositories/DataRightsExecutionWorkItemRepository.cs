namespace BunkFy.Modules.DataRights.Persistence.Repositories;

using BunkFy.Modules.DataRights.Application.Models;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Models;
using BunkFy.Modules.DataRights.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

internal sealed class DataRightsExecutionWorkItemRepository(DataRightsDbContext dbContext)
    : IDataRightsExecutionWorkItemRepository
{
    public Task AddAsync(
        DataRightsExecutionWorkItem workItem,
        CancellationToken cancellationToken)
    {
        dbContext.ExecutionWorkItems.Add(workItem);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyCollection<DataRightsExecutionWorkItem>> ListByBatchAsync(
        DataRightsCaseScope scope,
        Guid caseId,
        Guid batchId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        DataRightsCaseScopeKind scopeKind = scope.IsTenant
            ? DataRightsCaseScopeKind.Tenant
            : DataRightsCaseScopeKind.Property;
        return await dbContext.ExecutionWorkItems
            .Where(workItem =>
                workItem.CaseKind ==
                    (DataRightsCaseKind)scope.CaseType &&
                workItem.ScopeKind == scopeKind &&
                workItem.PropertyId == scope.PropertyId &&
                workItem.CaseId == caseId &&
                workItem.BatchId == batchId)
            .OrderBy(workItem => workItem.OwnerKey)
            .ThenBy(workItem => workItem.RecordType)
            .ThenBy(workItem => workItem.RecordId)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public Task<DataRightsExecutionWorkItem?> GetAsync(
        DataRightsCaseScope scope,
        Guid caseId,
        Guid workItemId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        DataRightsCaseScopeKind scopeKind = scope.IsTenant
            ? DataRightsCaseScopeKind.Tenant
            : DataRightsCaseScopeKind.Property;
        return dbContext.ExecutionWorkItems.SingleOrDefaultAsync(
            workItem =>
                workItem.CaseKind ==
                    (DataRightsCaseKind)scope.CaseType &&
                workItem.ScopeKind == scopeKind &&
                workItem.PropertyId == scope.PropertyId &&
                workItem.CaseId == caseId &&
                workItem.Id == workItemId,
            cancellationToken);
    }
}

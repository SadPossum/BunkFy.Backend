namespace BunkFy.Modules.DataRights.Persistence.Repositories;

using BunkFy.Modules.DataRights.Application.Models;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Models;
using BunkFy.Modules.DataRights.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

internal sealed class DataRightsExecutionBatchRepository(DataRightsDbContext dbContext)
    : IDataRightsExecutionBatchRepository
{
    public Task AddAsync(
        DataRightsExecutionBatch batch,
        CancellationToken cancellationToken)
    {
        dbContext.ExecutionBatches.Add(batch);
        return Task.CompletedTask;
    }

    public Task<DataRightsExecutionBatch?> GetByCaseAsync(
        DataRightsCaseScope scope,
        Guid caseId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        DataRightsCaseScopeKind scopeKind = scope.IsTenant
            ? DataRightsCaseScopeKind.Tenant
            : DataRightsCaseScopeKind.Property;
        return dbContext.ExecutionBatches.SingleOrDefaultAsync(
            batch =>
                batch.CaseKind == (DataRightsCaseKind)scope.CaseType &&
                batch.ScopeKind == scopeKind &&
                batch.PropertyId == scope.PropertyId &&
                batch.CaseId == caseId,
            cancellationToken);
    }
}

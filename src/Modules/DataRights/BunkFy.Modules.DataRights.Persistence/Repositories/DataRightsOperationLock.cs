namespace BunkFy.Modules.DataRights.Persistence.Repositories;

using BunkFy.Modules.DataRights.Application.Ports;
using Gma.Framework.Naming;
using Gma.Framework.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

internal sealed class DataRightsOperationLock(DataRightsDbContext dbContext)
    : IDataRightsOperationLock
{
    private const string ResourcePrefix = "bunkfy:data-rights:";

    public Task AcquireTenantControlAsync(
        CancellationToken cancellationToken) => this.AcquireAsync(
        "tenant-control",
        EfTransactionKeyLockMode.Exclusive,
        cancellationToken);

    public Task AcquireCaseReadAsync(
        Guid caseId,
        CancellationToken cancellationToken) => this.AcquireCaseAsync(
        caseId,
        EfTransactionKeyLockMode.Shared,
        cancellationToken);

    public Task AcquireCaseWriteAsync(
        Guid caseId,
        CancellationToken cancellationToken) => this.AcquireCaseAsync(
        caseId,
        EfTransactionKeyLockMode.Exclusive,
        cancellationToken);

    public Task AcquireExecutionWorkItemAsync(
        Guid workItemId,
        CancellationToken cancellationToken) => this.AcquireChildAsync(
        "execution-work-item",
        workItemId,
        cancellationToken);

    public Task AcquireProcessingLedgerAsync(
        CancellationToken cancellationToken) => this.AcquireAsync(
        "processing-ledger",
        EfTransactionKeyLockMode.Exclusive,
        cancellationToken);

    private Task AcquireCaseAsync(
        Guid caseId,
        EfTransactionKeyLockMode mode,
        CancellationToken cancellationToken)
    {
        if (caseId == Guid.Empty)
        {
            throw new ArgumentException(
                "A Data Rights case lock requires a valid identifier.",
                nameof(caseId));
        }

        return this.AcquireAsync(
            "case:" + caseId.ToString("N"),
            mode,
            cancellationToken);
    }

    public Task AcquireProcessReadAsync(
        Guid processId,
        CancellationToken cancellationToken) => this.AcquireProcessAsync(
        processId,
        EfTransactionKeyLockMode.Shared,
        cancellationToken);

    public Task AcquireProcessWriteAsync(
        Guid processId,
        CancellationToken cancellationToken) => this.AcquireProcessAsync(
        processId,
        EfTransactionKeyLockMode.Exclusive,
        cancellationToken);

    public Task AcquireOwnerWorkItemAsync(
        Guid workItemId,
        CancellationToken cancellationToken) => this.AcquireChildAsync(
        "owner-work",
        workItemId,
        cancellationToken);

    private Task AcquireChildAsync(
        string kind,
        Guid childId,
        CancellationToken cancellationToken)
    {
        if (childId == Guid.Empty)
        {
            throw new ArgumentException(
                "A Data Rights child lock requires a valid identifier.",
                nameof(childId));
        }

        return this.AcquireAsync(
            kind + ':' + childId.ToString("N"),
            EfTransactionKeyLockMode.Exclusive,
            cancellationToken);
    }

    private Task AcquireProcessAsync(
        Guid processId,
        EfTransactionKeyLockMode mode,
        CancellationToken cancellationToken)
    {
        if (processId == Guid.Empty)
        {
            throw new ArgumentException(
                "A Data Rights process lock requires a valid identifier.",
                nameof(processId));
        }

        return this.AcquireAsync(
            "process:" + processId.ToString("N"),
            mode,
            cancellationToken);
    }

    private Task AcquireAsync(
        string coordinate,
        EfTransactionKeyLockMode mode,
        CancellationToken cancellationToken)
    {
        if (!dbContext.ScopeFilterEnabled ||
            !TenantIds.TryNormalize(
                dbContext.CurrentScopeId,
                out string? tenantId))
        {
            throw new InvalidOperationException(
                "A Data Rights operation lock requires the current tenant scope.");
        }

        if (!dbContext.Database.IsRelational())
        {
            return Task.CompletedTask;
        }

        if (dbContext.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "A Data Rights operation lock requires an active database transaction.");
        }

        return EfTransactionKeyLock.AcquireAsync(
            dbContext,
            ResourcePrefix + tenantId + ':' + coordinate,
            mode,
            cancellationToken);
    }
}

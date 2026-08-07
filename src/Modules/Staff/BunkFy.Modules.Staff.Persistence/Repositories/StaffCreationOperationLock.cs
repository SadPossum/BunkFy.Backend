namespace BunkFy.Modules.Staff.Persistence.Repositories;

using BunkFy.Modules.Staff.Application.Ports;
using Gma.Framework.Naming;
using Gma.Framework.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

internal sealed class StaffCreationOperationLock(StaffDbContext dbContext)
    : IStaffCreationOperationLock
{
    private const string ResourcePrefix = "bunkfy:staff:member-create:";

    public async Task AcquireAsync(
        string tenantId,
        Guid operationId,
        CancellationToken cancellationToken)
    {
        if (!TenantIds.TryNormalize(tenantId, out string? canonicalTenantId) ||
            !string.Equals(
                canonicalTenantId,
                dbContext.CurrentScopeId,
                StringComparison.Ordinal) ||
            operationId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "A scoped Staff creation lock requires valid coordinates.");
        }

        if (dbContext.Database.IsRelational() &&
            dbContext.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "A Staff creation lock requires an active database transaction.");
        }

        await dbContext.AcquireOperationalMutationAdmissionAsync(
                cancellationToken)
            .ConfigureAwait(false);
        if (!dbContext.Database.IsRelational())
        {
            return;
        }

        await EfTransactionKeyLock.AcquireAsync(
                dbContext,
                ResourcePrefix + canonicalTenantId + ':' +
                operationId.ToString("N"),
                EfTransactionKeyLockMode.Exclusive,
                cancellationToken)
            .ConfigureAwait(false);
    }
}

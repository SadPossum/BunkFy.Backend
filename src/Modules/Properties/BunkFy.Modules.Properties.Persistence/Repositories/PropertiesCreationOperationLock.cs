namespace BunkFy.Modules.Properties.Persistence.Repositories;

using BunkFy.Modules.Properties.Application.Ports;
using Gma.Framework.Naming;
using Gma.Framework.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

internal sealed class PropertiesCreationOperationLock(
    PropertiesDbContext dbContext)
    : IPropertiesCreationOperationLock
{
    private const string ResourcePrefix =
        "bunkfy:properties:property-create:";

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
                "A scoped Properties creation lock requires valid coordinates.");
        }

        if (dbContext.Database.IsRelational() &&
            dbContext.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "A Properties creation lock requires an active database transaction.");
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

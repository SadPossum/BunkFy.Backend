namespace BunkFy.Modules.Inventory.Persistence.Repositories;

using BunkFy.Modules.Inventory.Application.Ports;
using Gma.Framework.Naming;
using Gma.Framework.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

internal sealed class InventoryManagementLock(InventoryDbContext dbContext)
    : IInventoryManagementLock
{
    private const string ResourcePrefix =
        "bunkfy:inventory:management-resource:";
    private const string OperationPrefix =
        "bunkfy:inventory:management-operation:";

    public Task AcquireResourceAsync(
        string tenantId,
        InventoryManagementResourceKind resourceKind,
        Guid resourceId,
        CancellationToken cancellationToken) => this.AcquireAsync(
            tenantId,
            resourceKind,
            resourceId,
            operationId: null,
            cancellationToken);

    public Task AcquireOperationAsync(
        string tenantId,
        InventoryManagementResourceKind resourceKind,
        Guid resourceId,
        Guid operationId,
        CancellationToken cancellationToken) => this.AcquireAsync(
            tenantId,
            resourceKind,
            resourceId,
            operationId,
            cancellationToken);

    private async Task AcquireAsync(
        string tenantId,
        InventoryManagementResourceKind resourceKind,
        Guid resourceId,
        Guid? operationId,
        CancellationToken cancellationToken)
    {
        if (!TenantIds.TryNormalize(tenantId, out string? canonicalTenantId) ||
            !string.Equals(
                canonicalTenantId,
                dbContext.CurrentScopeId,
                StringComparison.Ordinal) ||
            !Enum.IsDefined(resourceKind) ||
            resourceId == Guid.Empty ||
            (operationId.HasValue && operationId.Value == Guid.Empty))
        {
            throw new InvalidOperationException(
                "A scoped Inventory management lock requires valid coordinates.");
        }

        if (dbContext.Database.IsRelational() &&
            dbContext.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "An Inventory management lock requires an active database transaction.");
        }

        await dbContext.AcquireOperationalMutationAdmissionAsync(
                cancellationToken)
            .ConfigureAwait(false);
        if (!dbContext.Database.IsRelational())
        {
            return;
        }

        string coordinate = $"{(int)resourceKind}:{resourceId:N}";
        string key = operationId.HasValue
            ? OperationPrefix + canonicalTenantId + ':' + coordinate + ':' +
                operationId.Value.ToString("N")
            : ResourcePrefix + canonicalTenantId + ':' + coordinate;
        await EfTransactionKeyLock.AcquireAsync(
                dbContext,
                key,
                EfTransactionKeyLockMode.Exclusive,
                cancellationToken)
            .ConfigureAwait(false);
    }
}

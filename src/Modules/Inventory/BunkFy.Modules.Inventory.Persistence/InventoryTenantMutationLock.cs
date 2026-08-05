namespace BunkFy.Modules.Inventory.Persistence;

using BunkFy.Modules.DataRights.Contracts;
using Gma.Framework.Naming;
using Gma.Framework.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

internal static class InventoryTenantMutationLock
{
    private const string DestroyOperationResourcePrefix =
        "bunkfy:inventory:tenant-destroy-operation:";

    public static Task AcquireAdmissionAsync(
        InventoryDbContext dbContext,
        string tenantId,
        CancellationToken cancellationToken) =>
        AcquireAsync(
            dbContext,
            tenantId,
            EfTransactionKeyLockMode.Shared,
            cancellationToken);

    public static Task AcquireExclusiveAsync(
        InventoryDbContext dbContext,
        string tenantId,
        CancellationToken cancellationToken) =>
        AcquireAsync(
            dbContext,
            tenantId,
            EfTransactionKeyLockMode.Exclusive,
            cancellationToken);

    private static Task AcquireAsync(
        InventoryDbContext dbContext,
        string tenantId,
        EfTransactionKeyLockMode mode,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        if (!TenantIds.TryNormalize(tenantId, out string? canonicalTenantId))
        {
            throw new InventoryOperationalAdmissionException(
                InventoryOperationalAdmissionFailure.Unavailable);
        }

        return dbContext.Database.IsRelational()
            ? EfTransactionKeyLock.AcquireAsync(
                dbContext,
                TenantTerminationCoordination
                    .CreateTenantMutationResource(canonicalTenantId),
                mode,
                cancellationToken)
            : Task.CompletedTask;
    }

    public static async Task AcquireLifecycleAsync(
        InventoryDbContext dbContext,
        string tenantId,
        Guid operationId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        if (operationId == Guid.Empty)
        {
            throw new ArgumentException(
                "The tenant destruction operation id is required.",
                nameof(operationId));
        }

        if (!dbContext.Database.IsRelational())
        {
            return;
        }

        await EfTransactionKeyLock.AcquireAsync(
                dbContext,
                DestroyOperationResourcePrefix + operationId.ToString("D"),
                EfTransactionKeyLockMode.Exclusive,
                cancellationToken)
            .ConfigureAwait(false);
        await AcquireExclusiveAsync(dbContext, tenantId, cancellationToken)
            .ConfigureAwait(false);
    }
}

internal enum InventoryOperationalAdmissionFailure
{
    Restricted = 1,
    Unavailable = 2
}

internal sealed class InventoryOperationalAdmissionException(
    InventoryOperationalAdmissionFailure failure)
    : InvalidOperationException(
        failure == InventoryOperationalAdmissionFailure.Restricted
            ? "The workspace is not accepting Inventory mutations."
            : "Workspace mutation admission is unavailable.")
{
    public InventoryOperationalAdmissionFailure Failure { get; } = failure;
}

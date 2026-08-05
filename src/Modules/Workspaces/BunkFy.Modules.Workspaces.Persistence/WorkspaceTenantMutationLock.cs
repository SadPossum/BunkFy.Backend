namespace BunkFy.Modules.Workspaces.Persistence;

using BunkFy.Modules.DataRights.Contracts;
using Gma.Framework.Naming;
using Gma.Framework.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

internal static class WorkspaceTenantMutationLock
{
    private const string DestroyOperationResourcePrefix =
        "bunkfy:workspaces:tenant-destroy-operation:";

    public static Task AcquireAdmissionAsync(
        WorkspacesDbContext dbContext,
        string tenantId,
        CancellationToken cancellationToken) =>
        AcquireAsync(
            dbContext,
            tenantId,
            EfTransactionKeyLockMode.Shared,
            cancellationToken);

    public static Task AcquireExclusiveAsync(
        WorkspacesDbContext dbContext,
        string tenantId,
        CancellationToken cancellationToken) =>
        AcquireAsync(
            dbContext,
            tenantId,
            EfTransactionKeyLockMode.Exclusive,
            cancellationToken);

    private static Task AcquireAsync(
        WorkspacesDbContext dbContext,
        string tenantId,
        EfTransactionKeyLockMode mode,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        if (!TenantIds.TryNormalize(tenantId, out string? normalizedTenantId))
        {
            throw new InvalidOperationException(
                "The Workspaces tenant-mutation lock requires a valid tenant.");
        }

        if (!dbContext.Database.IsRelational())
        {
            return Task.CompletedTask;
        }

        return EfTransactionKeyLock.AcquireAsync(
            dbContext,
            TenantTerminationCoordination.CreateTenantMutationResource(
                normalizedTenantId),
            mode,
            cancellationToken);
    }

    public static async Task AcquireLifecycleAsync(
        WorkspacesDbContext dbContext,
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

internal sealed class WorkspaceOperationalMutationRejectedException()
    : InvalidOperationException(
        "The workspace is not accepting operational mutations.");

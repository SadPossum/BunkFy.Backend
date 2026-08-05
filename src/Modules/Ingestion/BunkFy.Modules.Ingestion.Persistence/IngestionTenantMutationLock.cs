namespace BunkFy.Modules.Ingestion.Persistence;

using BunkFy.Modules.DataRights.Contracts;
using Gma.Framework.Naming;
using Gma.Framework.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

internal static class IngestionTenantMutationLock
{
    private const string DestroyOperationResourcePrefix =
        "bunkfy:ingestion:tenant-destroy-operation:";
    private const string RevisionAdvanceResourcePrefix =
        "bunkfy:ingestion:tenant-revision-advance:";

    public static Task AcquireAdmissionAsync(
        IngestionDbContext dbContext,
        string tenantId,
        CancellationToken cancellationToken) =>
        AcquireAsync(
            dbContext,
            tenantId,
            EfTransactionKeyLockMode.Shared,
            cancellationToken);

    public static Task AcquireExclusiveAsync(
        IngestionDbContext dbContext,
        string tenantId,
        CancellationToken cancellationToken) =>
        AcquireAsync(
            dbContext,
            tenantId,
            EfTransactionKeyLockMode.Exclusive,
            cancellationToken);

    public static Task AcquireRevisionAdvanceAsync(
        IngestionDbContext dbContext,
        string tenantId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        if (!TenantIds.TryNormalize(tenantId, out string? canonicalTenantId))
        {
            throw new IngestionOperationalAdmissionException(
                IngestionOperationalAdmissionFailure.Unavailable);
        }

        return dbContext.Database.IsRelational()
            ? EfTransactionKeyLock.AcquireAsync(
                dbContext,
                RevisionAdvanceResourcePrefix + canonicalTenantId,
                EfTransactionKeyLockMode.Exclusive,
                cancellationToken)
            : Task.CompletedTask;
    }

    private static Task AcquireAsync(
        IngestionDbContext dbContext,
        string tenantId,
        EfTransactionKeyLockMode mode,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        if (!TenantIds.TryNormalize(tenantId, out string? canonicalTenantId))
        {
            throw new IngestionOperationalAdmissionException(
                IngestionOperationalAdmissionFailure.Unavailable);
        }

        return dbContext.Database.IsRelational()
            ? EfTransactionKeyLock.AcquireAsync(
                dbContext,
                TenantTerminationCoordination.CreateTenantMutationResource(
                    canonicalTenantId),
                mode,
                cancellationToken)
            : Task.CompletedTask;
    }

    public static async Task AcquireLifecycleAsync(
        IngestionDbContext dbContext,
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

internal enum IngestionOperationalAdmissionFailure
{
    Restricted = 1,
    Unavailable = 2
}

internal sealed class IngestionOperationalAdmissionException(
    IngestionOperationalAdmissionFailure failure)
    : InvalidOperationException(
        failure == IngestionOperationalAdmissionFailure.Restricted
            ? "The workspace is not accepting Ingestion mutations."
            : "Workspace mutation admission is unavailable.")
{
    public IngestionOperationalAdmissionFailure Failure { get; } = failure;
}

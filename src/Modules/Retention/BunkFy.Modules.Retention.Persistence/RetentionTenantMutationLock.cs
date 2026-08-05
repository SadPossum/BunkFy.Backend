namespace BunkFy.Modules.Retention.Persistence;

using BunkFy.Modules.DataRights.Contracts;
using Gma.Framework.Naming;
using Gma.Framework.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

internal static class RetentionTenantMutationLock
{
    private const string DestroyOperationResourcePrefix =
        "bunkfy:retention:tenant-destroy-operation:";
    private const string RevisionAdvanceResourcePrefix =
        "bunkfy:retention:tenant-revision-advance:";

    public static Task AcquireAdmissionAsync(
        RetentionDbContext dbContext,
        string tenantId,
        CancellationToken cancellationToken) =>
        AcquireAsync(
            dbContext,
            tenantId,
            EfTransactionKeyLockMode.Shared,
            cancellationToken);

    public static Task AcquireExclusiveAsync(
        RetentionDbContext dbContext,
        string tenantId,
        CancellationToken cancellationToken) =>
        AcquireAsync(
            dbContext,
            tenantId,
            EfTransactionKeyLockMode.Exclusive,
            cancellationToken);

    public static Task AcquireRevisionAdvanceAsync(
        RetentionDbContext dbContext,
        string tenantId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        if (!TenantIds.TryNormalize(tenantId, out string? canonicalTenantId))
        {
            throw new RetentionOperationalAdmissionException(
                RetentionOperationalAdmissionFailure.Unavailable);
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
        RetentionDbContext dbContext,
        string tenantId,
        EfTransactionKeyLockMode mode,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        if (!TenantIds.TryNormalize(tenantId, out string? canonicalTenantId))
        {
            throw new RetentionOperationalAdmissionException(
                RetentionOperationalAdmissionFailure.Unavailable);
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
        RetentionDbContext dbContext,
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

internal enum RetentionOperationalAdmissionFailure
{
    Restricted = 1,
    Unavailable = 2
}

internal sealed class RetentionOperationalAdmissionException(
    RetentionOperationalAdmissionFailure failure)
    : InvalidOperationException(
        failure == RetentionOperationalAdmissionFailure.Restricted
            ? "The workspace is not accepting Retention mutations."
            : "Workspace mutation admission is unavailable.")
{
    public RetentionOperationalAdmissionFailure Failure { get; } = failure;
}

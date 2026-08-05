namespace BunkFy.Modules.Guests.Persistence;

using BunkFy.Modules.DataRights.Contracts;
using Gma.Framework.Naming;
using Gma.Framework.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

internal static class GuestsTenantMutationLock
{
    private const string DestroyOperationResourcePrefix =
        "bunkfy:guests:tenant-destroy-operation:";

    public static Task AcquireAdmissionAsync(
        GuestsDbContext dbContext,
        string tenantId,
        CancellationToken cancellationToken) =>
        AcquireAsync(
            dbContext,
            tenantId,
            EfTransactionKeyLockMode.Shared,
            cancellationToken);

    public static Task AcquireExclusiveAsync(
        GuestsDbContext dbContext,
        string tenantId,
        CancellationToken cancellationToken) =>
        AcquireAsync(
            dbContext,
            tenantId,
            EfTransactionKeyLockMode.Exclusive,
            cancellationToken);

    private static Task AcquireAsync(
        GuestsDbContext dbContext,
        string tenantId,
        EfTransactionKeyLockMode mode,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        if (!TenantIds.TryNormalize(tenantId, out string? canonicalTenantId))
        {
            throw new GuestsOperationalAdmissionException(
                GuestsOperationalAdmissionFailure.Unavailable);
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
        GuestsDbContext dbContext,
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

internal enum GuestsOperationalAdmissionFailure
{
    Restricted = 1,
    Unavailable = 2
}

internal sealed class GuestsOperationalAdmissionException(
    GuestsOperationalAdmissionFailure failure)
    : InvalidOperationException(
        failure == GuestsOperationalAdmissionFailure.Restricted
            ? "The workspace is not accepting Guests mutations."
            : "Workspace mutation admission is unavailable.")
{
    public GuestsOperationalAdmissionFailure Failure { get; } = failure;
}

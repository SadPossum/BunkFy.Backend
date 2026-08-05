namespace BunkFy.Modules.Reservations.Persistence;

using BunkFy.Modules.DataRights.Contracts;
using Gma.Framework.Naming;
using Gma.Framework.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

internal static class ReservationsTenantMutationLock
{
    private const string DestroyOperationResourcePrefix =
        "bunkfy:reservations:tenant-destroy-operation:";
    private const string RevisionAdvanceResourcePrefix =
        "bunkfy:reservations:tenant-revision-advance:";

    public static Task AcquireAdmissionAsync(
        ReservationsDbContext dbContext,
        string tenantId,
        CancellationToken cancellationToken) =>
        AcquireAsync(
            dbContext,
            tenantId,
            EfTransactionKeyLockMode.Shared,
            cancellationToken);

    public static Task AcquireExclusiveAsync(
        ReservationsDbContext dbContext,
        string tenantId,
        CancellationToken cancellationToken) =>
        AcquireAsync(
            dbContext,
            tenantId,
            EfTransactionKeyLockMode.Exclusive,
            cancellationToken);

    public static Task AcquireRevisionAdvanceAsync(
        ReservationsDbContext dbContext,
        string tenantId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        if (!TenantIds.TryNormalize(tenantId, out string? canonicalTenantId))
        {
            throw new ReservationsOperationalAdmissionException(
                ReservationsOperationalAdmissionFailure.Unavailable);
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
        ReservationsDbContext dbContext,
        string tenantId,
        EfTransactionKeyLockMode mode,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        if (!TenantIds.TryNormalize(tenantId, out string? canonicalTenantId))
        {
            throw new ReservationsOperationalAdmissionException(
                ReservationsOperationalAdmissionFailure.Unavailable);
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
        ReservationsDbContext dbContext,
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

internal enum ReservationsOperationalAdmissionFailure
{
    Restricted = 1,
    Unavailable = 2
}

internal sealed class ReservationsOperationalAdmissionException(
    ReservationsOperationalAdmissionFailure failure)
    : InvalidOperationException(
        failure == ReservationsOperationalAdmissionFailure.Restricted
            ? "The workspace is not accepting Reservations mutations."
            : "Workspace mutation admission is unavailable.")
{
    public ReservationsOperationalAdmissionFailure Failure { get; } = failure;
}

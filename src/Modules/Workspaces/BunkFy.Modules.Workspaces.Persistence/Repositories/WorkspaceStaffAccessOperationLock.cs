namespace BunkFy.Modules.Workspaces.Persistence.Repositories;

using BunkFy.Modules.Workspaces.Application.Ports;
using Gma.Framework.Naming;
using Gma.Framework.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

internal sealed class WorkspaceStaffAccessOperationLock(
    WorkspacesDbContext dbContext)
    : IWorkspaceStaffAccessOperationLock
{
    private const string ResourcePrefix =
        "bunkfy:workspaces:staff-access:";

    public async Task AcquireStaffAsync(
        Guid staffMemberId,
        CancellationToken cancellationToken)
    {
        ValidateId(
            staffMemberId,
            nameof(staffMemberId),
            "staff member");
        await dbContext.AcquireOperationalMutationAdmissionAsync(
                cancellationToken).ConfigureAwait(false);
        await this.AcquireKeyAsync(staffMemberId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<bool> TryAcquireProcessAsync(
        Guid processId,
        CancellationToken cancellationToken)
    {
        ValidateId(processId, nameof(processId), "access process");
        await dbContext.AcquireOperationalMutationAdmissionAsync(
                cancellationToken).ConfigureAwait(false);

        Guid? staffMemberId = await dbContext.StaffAccessProcesses
            .AsNoTracking()
            .Where(process => process.Id == processId)
            .Select(process => (Guid?)process.StaffMemberId)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (staffMemberId is null)
        {
            return false;
        }

        await this.AcquireKeyAsync(
                staffMemberId.Value,
                cancellationToken).ConfigureAwait(false);
        return true;
    }

    private Task AcquireKeyAsync(
        Guid staffMemberId,
        CancellationToken cancellationToken)
    {
        if (!dbContext.Database.IsRelational())
        {
            return Task.CompletedTask;
        }

        if (dbContext.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "A Workspaces staff-access operation lock requires an active database transaction.");
        }

        if (!TenantIds.TryNormalize(
                dbContext.CurrentScopeId,
                out string? tenantId))
        {
            throw new InvalidOperationException(
                "A Workspaces staff-access operation lock requires the current tenant scope.");
        }

        return EfTransactionKeyLock.AcquireAsync(
            dbContext,
            ResourcePrefix + tenantId + ':' + staffMemberId.ToString("N"),
            EfTransactionKeyLockMode.Exclusive,
            cancellationToken);
    }

    private static void ValidateId(
        Guid value,
        string parameterName,
        string coordinate)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException(
                $"A Workspaces {coordinate} operation lock requires a valid identifier.",
                parameterName);
        }
    }
}

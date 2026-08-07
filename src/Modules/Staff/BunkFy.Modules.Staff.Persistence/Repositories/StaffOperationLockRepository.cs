namespace BunkFy.Modules.Staff.Persistence.Repositories;

using BunkFy.Modules.Staff.Application.Ports;
using BunkFy.Modules.Staff.Persistence.Models;
using Microsoft.EntityFrameworkCore;

internal sealed class StaffOperationLockRepository(
    StaffDbContext dbContext)
    : IStaffOperationLock
{
    public Task<long?> GetStaffMemberRevisionAsync(
        string tenantId,
        Guid staffMemberId,
        CancellationToken cancellationToken)
    {
        string scopeId = this.ValidateCoordinates(
            tenantId,
            staffMemberId);
        return dbContext.Set<StaffOperationLock>()
            .AsNoTracking()
            .Where(resourceLock =>
                resourceLock.ScopeId == scopeId &&
                resourceLock.StaffMemberId == staffMemberId)
            .Select(resourceLock => (long?)resourceLock.Revision)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> TryAcquireStaffMemberAsync(
        string tenantId,
        Guid staffMemberId,
        CancellationToken cancellationToken)
    {
        string scopeId = this.ValidateCoordinates(
            tenantId,
            staffMemberId);

        if (dbContext.Database.IsRelational() &&
            dbContext.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "A Staff operation lock requires an active database transaction.");
        }

        if (dbContext.Database.IsRelational())
        {
            await dbContext.AcquireOperationalMutationAdmissionAsync(
                    cancellationToken)
                .ConfigureAwait(false);
            int affected = await dbContext.Set<StaffOperationLock>()
                .Where(resourceLock =>
                    resourceLock.ScopeId == scopeId &&
                    resourceLock.StaffMemberId == staffMemberId)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(
                        resourceLock => resourceLock.Revision,
                        resourceLock => resourceLock.Revision + 1),
                    cancellationToken)
                .ConfigureAwait(false);
            if (affected != 1)
            {
                bool memberExists = await dbContext.StaffMembers
                    .AsNoTracking()
                    .AnyAsync(
                        member => member.Id == staffMemberId,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (!memberExists)
                {
                    return false;
                }

                throw new InvalidOperationException(
                    "The Staff operation lock is not provisioned.");
            }

            return true;
        }

        StaffOperationLock? existing =
            await dbContext.Set<StaffOperationLock>()
                .SingleOrDefaultAsync(
                    resourceLock =>
                        resourceLock.StaffMemberId == staffMemberId,
                    cancellationToken)
                .ConfigureAwait(false);
        if (existing is null)
        {
            bool memberExists = await dbContext.StaffMembers
                .AsNoTracking()
                .AnyAsync(
                    member => member.Id == staffMemberId,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!memberExists)
            {
                return false;
            }

            throw new InvalidOperationException(
                "The Staff operation lock is not provisioned.");
        }

        existing.Touch();
        await dbContext.SaveChangesAsync(cancellationToken)
            .ConfigureAwait(false);
        return true;
    }

    private string ValidateCoordinates(
        string tenantId,
        Guid staffMemberId)
    {
        string scopeId = tenantId?.Trim() ?? string.Empty;
        if (scopeId.Length == 0 ||
            !string.Equals(
                scopeId,
                dbContext.CurrentScopeId,
                StringComparison.Ordinal) ||
            staffMemberId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "A scoped Staff operation lock requires valid coordinates.");
        }

        return scopeId;
    }
}

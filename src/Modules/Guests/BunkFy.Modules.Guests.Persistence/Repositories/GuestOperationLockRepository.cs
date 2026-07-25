namespace BunkFy.Modules.Guests.Persistence.Repositories;

using BunkFy.Modules.Guests.Application.Ports;
using BunkFy.Modules.Guests.Persistence.Models;
using Microsoft.EntityFrameworkCore;

internal sealed class GuestOperationLockRepository(GuestsDbContext dbContext)
    : IGuestOperationLock
{
    public Task AcquireGuestAsync(
        string tenantId,
        Guid guestId,
        CancellationToken cancellationToken) =>
        this.AcquireAsync(
            tenantId,
            GuestOperationLockKind.Guest,
            [guestId],
            cancellationToken);

    public Task AcquirePropertiesAsync(
        string tenantId,
        IReadOnlyCollection<Guid> propertyIds,
        CancellationToken cancellationToken) =>
        this.AcquireAsync(
            tenantId,
            GuestOperationLockKind.Property,
            propertyIds,
            cancellationToken);

    private async Task AcquireAsync(
        string tenantId,
        GuestOperationLockKind resourceKind,
        IReadOnlyCollection<Guid> resourceIds,
        CancellationToken cancellationToken)
    {
        string scopeId = tenantId?.Trim() ?? string.Empty;
        Guid[] ids = resourceIds
            .Where(resourceId => resourceId != Guid.Empty)
            .Distinct()
            .OrderBy(resourceId => resourceId)
            .ToArray();
        if (scopeId.Length == 0 ||
            !string.Equals(scopeId, dbContext.CurrentScopeId, StringComparison.Ordinal) ||
            ids.Length == 0)
        {
            throw new InvalidOperationException(
                "A scoped Guest operation lock requires valid resource coordinates.");
        }

        bool relational = dbContext.Database.IsRelational();
        if (relational && dbContext.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "A Guest operation lock requires an active database transaction.");
        }

        GuestOperationLock[] existing = await dbContext.Set<GuestOperationLock>()
            .Where(resourceLock =>
                resourceLock.ResourceKind == resourceKind &&
                ids.Contains(resourceLock.ResourceId))
            .OrderBy(resourceLock => resourceLock.ResourceId)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        HashSet<Guid> existingIds = existing
            .Select(resourceLock => resourceLock.ResourceId)
            .ToHashSet();
        foreach (GuestOperationLock resourceLock in existing)
        {
            resourceLock.Touch();
        }

        foreach (Guid resourceId in ids.Where(resourceId => !existingIds.Contains(resourceId)))
        {
            dbContext.Set<GuestOperationLock>().Add(new(
                Guid.NewGuid(),
                scopeId,
                resourceKind,
                resourceId));
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

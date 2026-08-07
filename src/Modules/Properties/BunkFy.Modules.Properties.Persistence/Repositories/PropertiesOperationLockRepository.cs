namespace BunkFy.Modules.Properties.Persistence.Repositories;

using BunkFy.Modules.Properties.Application.Ports;
using Gma.Framework.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

internal sealed class PropertiesOperationLockRepository(
    PropertiesDbContext dbContext)
    : IPropertiesOperationLock,
      IPropertiesUniqueCoordinateLock
{
    private const string PropertyCodePrefix =
        "bunkfy:properties:property-code:";
    private const string RoomNamePrefix =
        "bunkfy:properties:room-name:";

    public async Task<bool> TryAcquirePropertyAsync(
        string tenantId,
        Guid propertyId,
        CancellationToken cancellationToken)
    {
        string scopeId = this.ValidateAggregateCoordinates(
            tenantId,
            propertyId);
        this.EnsureTransaction();
        if (dbContext.Database.IsRelational())
        {
            await dbContext.AcquireOperationalMutationAdmissionAsync(
                    cancellationToken)
                .ConfigureAwait(false);
            int affected = await dbContext.PropertyOperationLocks
                .Where(resourceLock =>
                    resourceLock.ScopeId == scopeId &&
                    resourceLock.PropertyId == propertyId)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(
                        resourceLock => resourceLock.Revision,
                        resourceLock => resourceLock.Revision + 1),
                    cancellationToken).ConfigureAwait(false);
            return affected == 1 ||
                await this.HandleMissingPropertyLockAsync(
                    propertyId,
                    cancellationToken).ConfigureAwait(false);
        }

        PropertyOperationLock? existing =
            await dbContext.PropertyOperationLocks
                .SingleOrDefaultAsync(
                    resourceLock =>
                        resourceLock.PropertyId == propertyId,
                    cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return await this.HandleMissingPropertyLockAsync(
                propertyId,
                cancellationToken).ConfigureAwait(false);
        }

        existing.Touch();
        await dbContext.SaveChangesAsync(cancellationToken)
            .ConfigureAwait(false);
        return true;
    }

    public async Task<bool> TryAcquireRoomAsync(
        string tenantId,
        Guid roomId,
        CancellationToken cancellationToken)
    {
        string scopeId = this.ValidateAggregateCoordinates(tenantId, roomId);
        this.EnsureTransaction();
        if (dbContext.Database.IsRelational())
        {
            await dbContext.AcquireOperationalMutationAdmissionAsync(
                    cancellationToken)
                .ConfigureAwait(false);
            int affected = await dbContext.RoomOperationLocks
                .Where(resourceLock =>
                    resourceLock.ScopeId == scopeId &&
                    resourceLock.RoomId == roomId)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(
                        resourceLock => resourceLock.Revision,
                        resourceLock => resourceLock.Revision + 1),
                    cancellationToken).ConfigureAwait(false);
            return affected == 1 ||
                await this.HandleMissingRoomLockAsync(
                    roomId,
                    cancellationToken).ConfigureAwait(false);
        }

        RoomOperationLock? existing = await dbContext.RoomOperationLocks
            .SingleOrDefaultAsync(
                resourceLock => resourceLock.RoomId == roomId,
                cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return await this.HandleMissingRoomLockAsync(
                roomId,
                cancellationToken).ConfigureAwait(false);
        }

        existing.Touch();
        await dbContext.SaveChangesAsync(cancellationToken)
            .ConfigureAwait(false);
        return true;
    }

    public Task AcquirePropertyCodeAsync(
        string tenantId,
        string propertyCode,
        CancellationToken cancellationToken)
    {
        string scopeId = this.ValidateUniqueCoordinates(
            tenantId,
            propertyCode);
        return this.AcquireUniqueCoordinateAsync(
            PropertyCodePrefix + scopeId + ':' + propertyCode,
            cancellationToken);
    }

    public Task AcquireRoomNameAsync(
        string tenantId,
        Guid propertyId,
        string roomName,
        CancellationToken cancellationToken)
    {
        if (propertyId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "A Properties room-name lock requires a valid Property coordinate.");
        }

        string scopeId = this.ValidateUniqueCoordinates(
            tenantId,
            roomName);
        return this.AcquireUniqueCoordinateAsync(
            RoomNamePrefix + scopeId + ':' +
                propertyId.ToString("N") + ':' + roomName,
            cancellationToken);
    }

    private async Task AcquireUniqueCoordinateAsync(
        string resource,
        CancellationToken cancellationToken)
    {
        if (!dbContext.Database.IsRelational())
        {
            return;
        }

        this.EnsureTransaction();
        await EfTransactionKeyLock.AcquireAsync(
            dbContext,
            resource,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> HandleMissingPropertyLockAsync(
        Guid propertyId,
        CancellationToken cancellationToken)
    {
        bool exists = await dbContext.Properties
            .AsNoTracking()
            .AnyAsync(
                property => property.Id == propertyId,
                cancellationToken).ConfigureAwait(false);
        if (!exists)
        {
            return false;
        }

        throw new InvalidOperationException(
            "The Property operation lock is not provisioned.");
    }

    private async Task<bool> HandleMissingRoomLockAsync(
        Guid roomId,
        CancellationToken cancellationToken)
    {
        bool exists = await dbContext.Rooms
            .AsNoTracking()
            .AnyAsync(
                room => room.Id == roomId,
                cancellationToken).ConfigureAwait(false);
        if (!exists)
        {
            return false;
        }

        throw new InvalidOperationException(
            "The Room operation lock is not provisioned.");
    }

    private string ValidateAggregateCoordinates(
        string tenantId,
        Guid aggregateId)
    {
        string scopeId = this.ValidateScope(tenantId);
        if (aggregateId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "A Properties operation lock requires a valid aggregate coordinate.");
        }

        return scopeId;
    }

    private string ValidateUniqueCoordinates(
        string tenantId,
        string value)
    {
        string scopeId = this.ValidateScope(tenantId);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                "A Properties uniqueness lock requires a valid coordinate.");
        }

        return scopeId;
    }

    private string ValidateScope(string tenantId)
    {
        string scopeId = tenantId?.Trim() ?? string.Empty;
        if (scopeId.Length == 0 ||
            !string.Equals(
                scopeId,
                dbContext.CurrentScopeId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "A scoped Properties lock requires an active tenant coordinate.");
        }

        return scopeId;
    }

    private void EnsureTransaction()
    {
        if (dbContext.Database.IsRelational() &&
            dbContext.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "A Properties lock requires an active database transaction.");
        }
    }
}

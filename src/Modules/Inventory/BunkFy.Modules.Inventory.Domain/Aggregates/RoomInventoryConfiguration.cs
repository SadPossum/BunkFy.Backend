namespace BunkFy.Modules.Inventory.Domain.Aggregates;

using Gma.Framework.Domain.Models;
using Gma.Framework.Results;
using BunkFy.Modules.Inventory.Domain.Errors;
using BunkFy.Modules.Inventory.Domain.Events;

public sealed class RoomInventoryConfiguration : ScopedAggregateRoot<Guid>
{
    private RoomInventoryConfiguration() { }

    private RoomInventoryConfiguration(Guid roomId, string scopeId, Guid propertyId, DateTimeOffset createdAtUtc)
        : base(roomId, scopeId)
    {
        this.PropertyId = propertyId;
        this.CreatedAtUtc = createdAtUtc;
    }

    public Guid PropertyId { get; private set; }
    public RoomSalesMode SalesMode { get; private set; } = RoomSalesMode.Unconfigured;
    public long Version { get; private set; } = 1;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? UpdatedAtUtc { get; private set; }

    public static Result<RoomInventoryConfiguration> Create(
        Guid roomId,
        string scopeId,
        Guid propertyId,
        DateTimeOffset nowUtc)
    {
        if (roomId == Guid.Empty)
        {
            return Result.Failure<RoomInventoryConfiguration>(InventoryDomainErrors.RoomIdRequired);
        }

        if (propertyId == Guid.Empty)
        {
            return Result.Failure<RoomInventoryConfiguration>(InventoryDomainErrors.PropertyIdRequired);
        }

        return Result.Success(new RoomInventoryConfiguration(roomId, scopeId, propertyId, nowUtc));
    }

    public Result Configure(
        RoomSalesMode salesMode,
        long expectedVersion,
        Guid eventId,
        DateTimeOffset nowUtc)
    {
        if (expectedVersion != this.Version)
        {
            return Result.Failure(InventoryDomainErrors.VersionConflict);
        }

        if (salesMode is not (RoomSalesMode.RoomLevel or RoomSalesMode.BedLevel))
        {
            return Result.Failure(InventoryDomainErrors.SalesModeInvalid);
        }

        if (this.SalesMode == salesMode)
        {
            return Result.Success();
        }

        this.SalesMode = salesMode;
        this.Version++;
        this.UpdatedAtUtc = nowUtc;
        this.RaiseDomainEvent(new RoomSalesModeChangedDomainEvent(
            eventId,
            nowUtc,
            this.ScopeId,
            this.PropertyId,
            this.Id,
            this.SalesMode,
            this.Version));

        return Result.Success();
    }
}

namespace BunkFy.Modules.Inventory.Domain.Aggregates;

using BunkFy.Modules.Inventory.Domain.Errors;
using BunkFy.Modules.Inventory.Domain.Events;
using Gma.Framework.Domain.Models;
using Gma.Framework.Results;

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
    public long AvailabilityMutationVersion { get; private set; } = 1;
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
        DateTimeOffset nowUtc,
        string? actorId = null)
    {
        Result<RoomSalesModeConfigurationOutcome> evaluation =
            this.EvaluateConfiguration(salesMode, expectedVersion);
        if (evaluation.IsFailure)
        {
            return Result.Failure(evaluation.Error);
        }

        if (evaluation.Value == RoomSalesModeConfigurationOutcome.Unchanged)
        {
            return Result.Success();
        }

        if (eventId == Guid.Empty)
        {
            return Result.Failure(InventoryDomainErrors.EventIdRequired);
        }

        this.SalesMode = salesMode;
        this.Version++;
        this.AvailabilityMutationVersion++;
        this.UpdatedAtUtc = nowUtc;
        this.RaiseDomainEvent(new RoomSalesModeChangedDomainEvent(
            eventId,
            nowUtc,
            this.ScopeId,
            this.PropertyId,
            this.Id,
            this.SalesMode,
            this.Version,
            actorId));

        return Result.Success();
    }

    public Result<RoomSalesModeConfigurationOutcome> EvaluateConfiguration(
        RoomSalesMode salesMode,
        long expectedVersion)
    {
        if (expectedVersion != this.Version)
        {
            return Result.Failure<RoomSalesModeConfigurationOutcome>(
                InventoryDomainErrors.VersionConflict);
        }

        if (salesMode is not (
            RoomSalesMode.RoomLevel or RoomSalesMode.BedLevel))
        {
            return Result.Failure<RoomSalesModeConfigurationOutcome>(
                InventoryDomainErrors.SalesModeInvalid);
        }

        return Result.Success(
            this.SalesMode == salesMode
                ? RoomSalesModeConfigurationOutcome.Unchanged
                : RoomSalesModeConfigurationOutcome.Changed);
    }

    public void TouchAvailability() => this.AvailabilityMutationVersion++;
}

public enum RoomSalesModeConfigurationOutcome
{
    Unchanged = 1,
    Changed = 2
}

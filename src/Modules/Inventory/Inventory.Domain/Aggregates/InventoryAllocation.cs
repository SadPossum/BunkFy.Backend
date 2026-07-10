namespace Inventory.Domain.Aggregates;

using Gma.Framework.Domain.Models;
using Gma.Framework.Results;
using Inventory.Domain.Entities;
using Inventory.Domain.Errors;

public sealed class InventoryAllocation : ScopedAggregateRoot<Guid>
{
    public const int MaximumUnits = 100;
    private readonly List<InventoryAllocationUnit> units = [];

    private InventoryAllocation() { }

    private InventoryAllocation(
        Guid allocationId,
        string scopeId,
        Guid reservationId,
        Guid allocationRequestId,
        Guid propertyId,
        DateOnly arrival,
        DateOnly departure,
        InventoryAllocationState status,
        InventoryAllocationRejection rejection,
        IReadOnlyCollection<Guid> inventoryUnitIds,
        DateTimeOffset createdAtUtc)
        : base(allocationId, scopeId)
    {
        this.ReservationId = reservationId;
        this.AllocationRequestId = allocationRequestId;
        this.PropertyId = propertyId;
        this.Arrival = arrival;
        this.Departure = departure;
        this.Status = status;
        this.Rejection = rejection;
        this.CreatedAtUtc = createdAtUtc;
        this.units.AddRange(inventoryUnitIds.Select(unitId => new InventoryAllocationUnit(unitId, this.ScopeId, this.Id)));
    }

    public Guid ReservationId { get; private set; }
    public Guid AllocationRequestId { get; private set; }
    public Guid PropertyId { get; private set; }
    public DateOnly Arrival { get; private set; }
    public DateOnly Departure { get; private set; }
    public InventoryAllocationState Status { get; private set; }
    public InventoryAllocationRejection Rejection { get; private set; }
    public long Version { get; private set; } = 1;
    public Guid? ReleaseRequestId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? ReleasedAtUtc { get; private set; }
    public IReadOnlyCollection<InventoryAllocationUnit> Units => this.units.AsReadOnly();

    public static Result<InventoryAllocation> CreateAccepted(
        Guid allocationId,
        string scopeId,
        Guid reservationId,
        Guid allocationRequestId,
        Guid propertyId,
        DateOnly arrival,
        DateOnly departure,
        IReadOnlyCollection<Guid> inventoryUnitIds,
        DateTimeOffset nowUtc) =>
        Create(
            allocationId,
            scopeId,
            reservationId,
            allocationRequestId,
            propertyId,
            arrival,
            departure,
            inventoryUnitIds,
            InventoryAllocationState.Active,
            InventoryAllocationRejection.None,
            nowUtc);

    public static Result<InventoryAllocation> CreateRejected(
        Guid allocationId,
        string scopeId,
        Guid reservationId,
        Guid allocationRequestId,
        Guid propertyId,
        DateOnly arrival,
        DateOnly departure,
        IReadOnlyCollection<Guid> inventoryUnitIds,
        InventoryAllocationRejection rejection,
        DateTimeOffset nowUtc) =>
        rejection == InventoryAllocationRejection.None
            ? Result.Failure<InventoryAllocation>(InventoryDomainErrors.AllocationRejectionRequired)
            : Create(
                allocationId,
                scopeId,
                reservationId,
                allocationRequestId,
                propertyId,
                arrival,
                departure,
                inventoryUnitIds,
                InventoryAllocationState.Rejected,
                rejection,
                nowUtc);

    public bool MatchesRequest(
        Guid reservationId,
        Guid propertyId,
        DateOnly arrival,
        DateOnly departure,
        IReadOnlyCollection<Guid> inventoryUnitIds) =>
        this.ReservationId == reservationId &&
        this.PropertyId == propertyId &&
        this.Arrival == arrival &&
        this.Departure == departure &&
        this.units.Select(unit => unit.InventoryUnitId).Order().SequenceEqual(inventoryUnitIds.Order());

    public Result Release(Guid releaseRequestId, long expectedVersion, DateTimeOffset nowUtc)
    {
        if (this.Status == InventoryAllocationState.Rejected)
        {
            return Result.Failure(InventoryDomainErrors.AllocationNotActive);
        }

        if (this.Status == InventoryAllocationState.Released)
        {
            return Result.Success();
        }

        if (expectedVersion != this.Version)
        {
            return Result.Failure(InventoryDomainErrors.VersionConflict);
        }

        if (releaseRequestId == Guid.Empty)
        {
            return Result.Failure(InventoryDomainErrors.AllocationReleaseRequestIdRequired);
        }

        this.Status = InventoryAllocationState.Released;
        this.ReleaseRequestId = releaseRequestId;
        this.ReleasedAtUtc = nowUtc;
        this.Version++;
        return Result.Success();
    }

    private static Result<InventoryAllocation> Create(
        Guid allocationId,
        string scopeId,
        Guid reservationId,
        Guid allocationRequestId,
        Guid propertyId,
        DateOnly arrival,
        DateOnly departure,
        IReadOnlyCollection<Guid> inventoryUnitIds,
        InventoryAllocationState status,
        InventoryAllocationRejection rejection,
        DateTimeOffset nowUtc)
    {
        if (allocationId == Guid.Empty)
        {
            return Result.Failure<InventoryAllocation>(InventoryDomainErrors.AllocationIdRequired);
        }

        if (reservationId == Guid.Empty)
        {
            return Result.Failure<InventoryAllocation>(InventoryDomainErrors.ReservationIdRequired);
        }

        if (allocationRequestId == Guid.Empty)
        {
            return Result.Failure<InventoryAllocation>(InventoryDomainErrors.AllocationRequestIdRequired);
        }

        if (propertyId == Guid.Empty)
        {
            return Result.Failure<InventoryAllocation>(InventoryDomainErrors.PropertyIdRequired);
        }

        if (arrival >= departure)
        {
            return Result.Failure<InventoryAllocation>(InventoryDomainErrors.StayRangeInvalid);
        }

        ArgumentNullException.ThrowIfNull(inventoryUnitIds);
        Guid[] units = inventoryUnitIds.ToArray();
        if (units.Length is 0 or > MaximumUnits || units.Any(id => id == Guid.Empty) || units.Distinct().Count() != units.Length)
        {
            return Result.Failure<InventoryAllocation>(InventoryDomainErrors.AllocationUnitsInvalid);
        }

        return Result.Success(new InventoryAllocation(
            allocationId,
            scopeId,
            reservationId,
            allocationRequestId,
            propertyId,
            arrival,
            departure,
            status,
            rejection,
            units,
            nowUtc));
    }
}

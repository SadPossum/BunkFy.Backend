namespace Inventory.Tests;

using Gma.Framework.Results;
using Inventory.Domain.Aggregates;
using Inventory.Domain.Errors;
using Xunit;

[Trait("Category", "Unit")]
public sealed class InventoryAllocationTests
{
    private static readonly Guid ReservationId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid RequestId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid PropertyId = Guid.Parse("30000000-0000-0000-0000-000000000001");
    private static readonly Guid UnitOne = Guid.Parse("40000000-0000-0000-0000-000000000001");
    private static readonly Guid UnitTwo = Guid.Parse("40000000-0000-0000-0000-000000000002");
    private static readonly DateTimeOffset Now = new(2026, 7, 10, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Accepted_allocation_is_order_insensitively_idempotent()
    {
        InventoryAllocation allocation = CreateAccepted([UnitOne, UnitTwo]).Value;

        Assert.Equal(InventoryAllocationState.Active, allocation.Status);
        Assert.Equal(1, allocation.Version);
        Assert.True(allocation.MatchesRequest(
            ReservationId,
            PropertyId,
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 3),
            [UnitTwo, UnitOne]));
    }

    [Fact]
    public void Rejected_allocation_requires_a_reason_and_preserves_request_units()
    {
        Result<InventoryAllocation> missingReason = InventoryAllocation.CreateRejected(
            Guid.NewGuid(),
            "tenant-a",
            ReservationId,
            RequestId,
            PropertyId,
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 3),
            [UnitOne],
            InventoryAllocationRejection.None,
            Now);
        InventoryAllocation rejected = InventoryAllocation.CreateRejected(
            Guid.NewGuid(),
            "tenant-a",
            ReservationId,
            RequestId,
            PropertyId,
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 3),
            [UnitOne],
            InventoryAllocationRejection.ManualBlockConflict,
            Now).Value;

        Assert.Equal(InventoryDomainErrors.AllocationRejectionRequired, missingReason.Error);
        Assert.Equal(InventoryAllocationState.Rejected, rejected.Status);
        Assert.Equal(UnitOne, Assert.Single(rejected.Units).InventoryUnitId);
    }

    [Fact]
    public void Release_is_versioned_and_idempotent_after_success()
    {
        InventoryAllocation allocation = CreateAccepted([UnitOne]).Value;
        Guid releaseRequestId = Guid.NewGuid();

        Assert.Equal(InventoryDomainErrors.VersionConflict, allocation.Release(releaseRequestId, 99, Now).Error);
        Assert.True(allocation.Release(releaseRequestId, 1, Now).IsSuccess);
        Assert.Equal(InventoryAllocationState.Released, allocation.Status);
        Assert.Equal(2, allocation.Version);
        Assert.True(allocation.Release(Guid.NewGuid(), 1, Now.AddMinutes(1)).IsSuccess);
        Assert.Equal(2, allocation.Version);
    }

    [Fact]
    public void Allocation_rejects_duplicate_units_and_empty_ranges()
    {
        Assert.Equal(
            InventoryDomainErrors.AllocationUnitsInvalid,
            CreateAccepted([UnitOne, UnitOne]).Error);
        Assert.Equal(
            InventoryDomainErrors.StayRangeInvalid,
            InventoryAllocation.CreateAccepted(
                Guid.NewGuid(),
                "tenant-a",
                ReservationId,
                RequestId,
                PropertyId,
                new DateOnly(2026, 8, 1),
                new DateOnly(2026, 8, 1),
                [UnitOne],
                Now).Error);
    }

    private static Result<InventoryAllocation> CreateAccepted(IReadOnlyCollection<Guid> units) =>
        InventoryAllocation.CreateAccepted(
            Guid.NewGuid(),
            "tenant-a",
            ReservationId,
            RequestId,
            PropertyId,
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 3),
            units,
            Now);
}

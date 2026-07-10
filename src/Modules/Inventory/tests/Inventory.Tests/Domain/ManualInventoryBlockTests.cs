namespace Inventory.Tests;

using Gma.Framework.Results;
using Inventory.Domain.Aggregates;
using Inventory.Domain.Errors;
using Inventory.Domain.Events;
using Xunit;

[Trait("Category", "Unit")]
public sealed class ManualInventoryBlockTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 10, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_uses_half_open_range_and_raises_versioned_event()
    {
        Result<ManualInventoryBlock> result = CreateBlock(
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 3),
            " Maintenance ");

        Assert.True(result.IsSuccess);
        Assert.Equal("Maintenance", result.Value.Reason);
        Assert.Equal(ManualInventoryBlockState.Active, result.Value.Status);
        Assert.Equal(1, result.Value.Version);
        ManualInventoryBlockCreatedDomainEvent domainEvent =
            Assert.IsType<ManualInventoryBlockCreatedDomainEvent>(Assert.Single(result.Value.DomainEvents));
        Assert.Equal(new DateOnly(2026, 8, 3), domainEvent.Departure);
        Assert.Equal(1, domainEvent.BlockVersion);
    }

    [Fact]
    public void Create_rejects_empty_ranges_and_reasons()
    {
        DateOnly date = new(2026, 8, 1);

        Assert.Equal(InventoryDomainErrors.StayRangeInvalid, CreateBlock(date, date, "Maintenance").Error);
        Assert.Equal(InventoryDomainErrors.BlockReasonInvalid, CreateBlock(date, date.AddDays(1), " ").Error);
    }

    [Fact]
    public void Release_is_versioned_and_terminal()
    {
        ManualInventoryBlock block = CreateBlock(
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 3),
            "Maintenance").Value;
        block.ClearDomainEvents();

        Result released = block.Release(1, Guid.NewGuid(), Now.AddMinutes(1));

        Assert.True(released.IsSuccess);
        Assert.Equal(ManualInventoryBlockState.Released, block.Status);
        Assert.Equal(2, block.Version);
        Assert.IsType<ManualInventoryBlockReleasedDomainEvent>(Assert.Single(block.DomainEvents));
        Assert.Equal(InventoryDomainErrors.BlockAlreadyReleased, block.Release(2, Guid.NewGuid(), Now).Error);
    }

    [Fact]
    public void Release_rejects_stale_version_without_mutation()
    {
        ManualInventoryBlock block = CreateBlock(
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 3),
            "Maintenance").Value;

        Assert.Equal(InventoryDomainErrors.VersionConflict, block.Release(99, Guid.NewGuid(), Now).Error);
        Assert.Equal(ManualInventoryBlockState.Active, block.Status);
        Assert.Equal(1, block.Version);
    }

    private static Result<ManualInventoryBlock> CreateBlock(DateOnly arrival, DateOnly departure, string reason) =>
        ManualInventoryBlock.Create(
            Guid.NewGuid(),
            "tenant-a",
            Guid.NewGuid(),
            Guid.NewGuid(),
            arrival,
            departure,
            reason,
            Guid.NewGuid(),
            Now);
}

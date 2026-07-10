namespace Inventory.Tests;

using Gma.Framework.Results;
using Inventory.Domain.Aggregates;
using Inventory.Domain.Errors;
using Inventory.Domain.Events;
using Xunit;

[Trait("Category", "Unit")]
public sealed class RoomInventoryConfigurationTests
{
    [Fact]
    public void Create_starts_unconfigured_at_version_one()
    {
        RoomInventoryConfiguration configuration = CreateConfiguration();

        Assert.Equal(RoomSalesMode.Unconfigured, configuration.SalesMode);
        Assert.Equal(1, configuration.Version);
        Assert.Empty(configuration.DomainEvents);
    }

    [Fact]
    public void Configure_changes_mode_and_raises_versioned_event()
    {
        RoomInventoryConfiguration configuration = CreateConfiguration();
        Guid eventId = Guid.NewGuid();

        Result result = configuration.Configure(RoomSalesMode.BedLevel, 1, eventId, Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(RoomSalesMode.BedLevel, configuration.SalesMode);
        Assert.Equal(2, configuration.Version);
        RoomSalesModeChangedDomainEvent domainEvent =
            Assert.IsType<RoomSalesModeChangedDomainEvent>(Assert.Single(configuration.DomainEvents));
        Assert.Equal(eventId, domainEvent.EventId);
        Assert.Equal(2, domainEvent.ConfigurationVersion);
    }

    [Fact]
    public void Configure_rejects_stale_or_non_sellable_modes_without_mutation()
    {
        RoomInventoryConfiguration configuration = CreateConfiguration();

        Assert.Equal(
            InventoryDomainErrors.VersionConflict,
            configuration.Configure(RoomSalesMode.RoomLevel, 99, Guid.NewGuid(), Now).Error);
        Assert.Equal(
            InventoryDomainErrors.SalesModeInvalid,
            configuration.Configure(RoomSalesMode.Unconfigured, 1, Guid.NewGuid(), Now).Error);
        Assert.Equal(RoomSalesMode.Unconfigured, configuration.SalesMode);
        Assert.Equal(1, configuration.Version);
        Assert.Empty(configuration.DomainEvents);
    }

    [Fact]
    public void Reapplying_current_mode_is_idempotent()
    {
        RoomInventoryConfiguration configuration = CreateConfiguration();
        configuration.Configure(RoomSalesMode.RoomLevel, 1, Guid.NewGuid(), Now);
        configuration.ClearDomainEvents();

        Result result = configuration.Configure(RoomSalesMode.RoomLevel, 2, Guid.NewGuid(), Now.AddMinutes(1));

        Assert.True(result.IsSuccess);
        Assert.Equal(2, configuration.Version);
        Assert.Empty(configuration.DomainEvents);
    }

    private static readonly DateTimeOffset Now = new(2026, 7, 10, 12, 0, 0, TimeSpan.Zero);

    private static RoomInventoryConfiguration CreateConfiguration() =>
        RoomInventoryConfiguration.Create(Guid.NewGuid(), "tenant-a", Guid.NewGuid(), Now).Value;
}

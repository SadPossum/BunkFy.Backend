namespace BunkFy.Modules.Inventory.Tests;

using Gma.Framework.Cqrs;
using Gma.Framework.Pagination;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using BunkFy.Modules.Inventory.Application;
using BunkFy.Modules.Inventory.Application.Commands;
using BunkFy.Modules.Inventory.Application.Ports;
using BunkFy.Modules.Inventory.Contracts;
using BunkFy.Modules.Inventory.Domain.Aggregates;
using BunkFy.Modules.Inventory.Domain.Errors;
using Microsoft.Extensions.DependencyInjection;
using BunkFy.Modules.Properties.Contracts;
using Xunit;

[Trait("Category", "Unit")]
public sealed class ConfigureRoomSalesModeCommandHandlerTests
{
    private static readonly Guid PropertyId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid RoomId = Guid.Parse("20000000-0000-0000-0000-000000000001");

    [Fact]
    public async Task Bed_level_requires_an_active_bed()
    {
        RoomInventoryConfiguration configuration = CreateConfiguration();
        ServiceProvider provider = CreateProvider(configuration, RoomStatus.Active, activeBedCount: 0);
        ICommandHandler<ConfigureRoomSalesModeCommand, RoomInventoryDto> handler =
            provider.GetRequiredService<ICommandHandler<ConfigureRoomSalesModeCommand, RoomInventoryDto>>();

        Result<RoomInventoryDto> result = await handler.HandleAsync(
            new(PropertyId, RoomId, InventorySalesMode.BedLevel, 1),
            CancellationToken.None);

        Assert.Equal(InventoryDomainErrors.BedLevelRequiresBeds, result.Error);
        Assert.Equal(RoomSalesMode.Unconfigured, configuration.SalesMode);
    }

    [Fact]
    public async Task Retired_room_cannot_be_configured()
    {
        RoomInventoryConfiguration configuration = CreateConfiguration();
        ServiceProvider provider = CreateProvider(configuration, RoomStatus.Retired, activeBedCount: 2);
        ICommandHandler<ConfigureRoomSalesModeCommand, RoomInventoryDto> handler =
            provider.GetRequiredService<ICommandHandler<ConfigureRoomSalesModeCommand, RoomInventoryDto>>();

        Result<RoomInventoryDto> result = await handler.HandleAsync(
            new(PropertyId, RoomId, InventorySalesMode.RoomLevel, 1),
            CancellationToken.None);

        Assert.Equal(InventoryDomainErrors.RoomRetired, result.Error);
        Assert.Equal(RoomSalesMode.Unconfigured, configuration.SalesMode);
    }

    [Fact]
    public async Task Valid_configuration_returns_the_materialized_room()
    {
        RoomInventoryConfiguration configuration = CreateConfiguration();
        ServiceProvider provider = CreateProvider(configuration, RoomStatus.Active, activeBedCount: 2);
        ICommandHandler<ConfigureRoomSalesModeCommand, RoomInventoryDto> handler =
            provider.GetRequiredService<ICommandHandler<ConfigureRoomSalesModeCommand, RoomInventoryDto>>();

        Result<RoomInventoryDto> result = await handler.HandleAsync(
            new(PropertyId, RoomId, InventorySalesMode.BedLevel, 1),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(InventorySalesMode.BedLevel, result.Value.SalesMode);
        Assert.Equal(2, result.Value.Version);
        Assert.Equal(RoomSalesMode.BedLevel, configuration.SalesMode);
    }

    private static ServiceProvider CreateProvider(
        RoomInventoryConfiguration configuration,
        RoomStatus status,
        int activeBedCount)
    {
        ServiceCollection services = new();
        services.AddSingleton<IInventoryTopologyRepository>(new FakeTopologyRepository(status, activeBedCount));
        services.AddSingleton<IRoomInventoryConfigurationRepository>(new FakeConfigurationRepository(configuration));
        services.AddSingleton<IInventoryReadRepository>(new FakeReadRepository(configuration));
        services.AddSingleton<ISystemClock>(new TestClock());
        services.AddSingleton<IIdGenerator>(new TestIdGenerator());
        services.AddInventoryApplication();
        return services.BuildServiceProvider();
    }

    private static RoomInventoryConfiguration CreateConfiguration() =>
        RoomInventoryConfiguration.Create(RoomId, "tenant-a", PropertyId, TestClock.Now).Value;

    private sealed class FakeTopologyRepository(RoomStatus status, int activeBedCount) : IInventoryTopologyRepository
    {
        public Task ApplyPropertyAsync(InventoryPropertyTopologyWriteModel property, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task ApplyRoomAsync(InventoryRoomTopologyWriteModel room, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task ApplyBedAsync(InventoryBedTopologyWriteModel bed, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<InventoryRoomTopologySnapshot?> GetRoomAsync(
            Guid propertyId,
            Guid roomId,
            CancellationToken cancellationToken) =>
            Task.FromResult<InventoryRoomTopologySnapshot?>(
                propertyId == PropertyId && roomId == RoomId
                    ? new(PropertyId, RoomId, status, activeBedCount)
                    : null);

        public Task<IReadOnlyCollection<InventoryUnitDefinitionSnapshot>> GetUnitDefinitionsAsync(
            Guid propertyId,
            Guid? roomId,
            Guid? inventoryUnitId,
            bool touchVersions,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<InventoryUnitDefinitionSnapshot>>([]);
    }

    private sealed class FakeConfigurationRepository(RoomInventoryConfiguration configuration)
        : IRoomInventoryConfigurationRepository
    {
        public Task EnsureAsync(
            string scopeId,
            Guid propertyId,
            Guid roomId,
            DateTimeOffset createdAtUtc,
            CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<RoomInventoryConfiguration?> GetAsync(
            Guid propertyId,
            Guid roomId,
            CancellationToken cancellationToken) =>
            Task.FromResult<RoomInventoryConfiguration?>(
                propertyId == PropertyId && roomId == RoomId ? configuration : null);
    }

    private sealed class FakeReadRepository(RoomInventoryConfiguration configuration) : IInventoryReadRepository
    {
        public Task<bool> PropertyExistsAsync(Guid propertyId, CancellationToken cancellationToken) =>
            Task.FromResult(propertyId == PropertyId);

        public Task<RoomInventoryDto?> GetRoomAsync(
            Guid propertyId,
            Guid roomId,
            CancellationToken cancellationToken) =>
            Task.FromResult<RoomInventoryDto?>(
                propertyId == PropertyId && roomId == RoomId
                    ? new(
                        PropertyId,
                        RoomId,
                        "101",
                        configuration.SalesMode == RoomSalesMode.BedLevel
                            ? InventorySalesMode.BedLevel
                            : InventorySalesMode.RoomLevel,
                        configuration.Version,
                        [])
                    : null);

        public Task<InventoryUnitSnapshot?> GetUnitAsync(
            Guid propertyId,
            Guid inventoryUnitId,
            CancellationToken cancellationToken) => Task.FromResult<InventoryUnitSnapshot?>(null);

        public Task<RoomInventoryListResponse> ListRoomsAsync(
            Guid propertyId,
            PageRequest pageRequest,
            CancellationToken cancellationToken) =>
            Task.FromResult(new RoomInventoryListResponse([], pageRequest.Page, pageRequest.PageSize));

        public Task<InventoryAvailabilityResponse> GetAvailabilityAsync(
            Guid propertyId,
            DateOnly arrival,
            DateOnly departure,
            CancellationToken cancellationToken) =>
            Task.FromResult(new InventoryAvailabilityResponse(propertyId, arrival, departure, []));
    }

    private sealed class TestClock : ISystemClock
    {
        public static DateTimeOffset Now { get; } = new(2026, 7, 10, 12, 0, 0, TimeSpan.Zero);
        public DateTimeOffset UtcNow => Now;
    }

    private sealed class TestIdGenerator : IIdGenerator
    {
        public Guid NewId() => Guid.CreateVersion7();
    }
}

namespace BunkFy.Modules.Properties.Tests;

using BunkFy.Modules.Properties.Application;
using BunkFy.Modules.Properties.Application.Commands;
using BunkFy.Modules.Properties.Application.Ports;
using BunkFy.Modules.Properties.Contracts;
using BunkFy.Modules.Properties.Domain.Aggregates;
using BunkFy.Modules.Properties.Domain.Errors;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

[Trait("Category", "Unit")]
public sealed class PropertiesLifecycleCommandHandlerTests
{
    [Fact]
    public async Task Property_retirement_is_blocked_while_rooms_are_active()
    {
        Property property = CreateProperty();
        FakeRoomRepository rooms = new() { HasActiveRooms = true };
        ServiceProvider provider = CreateProvider(property, rooms);
        ICommandHandler<RetirePropertyCommand, PropertyMutationReceiptDto> handler =
            provider.GetRequiredService<ICommandHandler<RetirePropertyCommand, PropertyMutationReceiptDto>>();

        Result<PropertyMutationReceiptDto> result = await handler.HandleAsync(
            new RetirePropertyCommand(
                property.Id,
                Guid.NewGuid(),
                true,
                property.Version),
            CancellationToken.None);

        Assert.Equal(PropertiesDomainErrors.PropertyHasActiveRooms, result.Error);
        Assert.Equal(PropertyState.Active, property.Status);
        Assert.Equal(1, property.Version);
    }

    [Fact]
    public async Task Property_retires_after_all_rooms_are_retired()
    {
        Property property = CreateProperty();
        ServiceProvider provider = CreateProvider(property, new FakeRoomRepository());
        ICommandHandler<RetirePropertyCommand, PropertyMutationReceiptDto> handler =
            provider.GetRequiredService<ICommandHandler<RetirePropertyCommand, PropertyMutationReceiptDto>>();

        Result<PropertyMutationReceiptDto> result = await handler.HandleAsync(
            new RetirePropertyCommand(
                property.Id,
                Guid.NewGuid(),
                true,
                property.Version),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(PropertyState.Retired, property.Status);
        Assert.Equal(2, property.Version);
        Assert.Equal(property.Version, result.Value.Version);
    }

    [Fact]
    public async Task Exact_retirement_replay_returns_the_original_receipt_without_rechecking_rooms()
    {
        Property property = CreateProperty();
        FakeRoomRepository rooms = new();
        RecordingPropertyMutationOperationRepository operations = new();
        ServiceProvider provider = CreateProvider(property, rooms, operations);
        ICommandHandler<RetirePropertyCommand, PropertyMutationReceiptDto> handler =
            provider.GetRequiredService<ICommandHandler<RetirePropertyCommand, PropertyMutationReceiptDto>>();
        RetirePropertyCommand command = new(
            property.Id,
            Guid.NewGuid(),
            true,
            property.Version);

        Result<PropertyMutationReceiptDto> first = await handler.HandleAsync(
            command,
            CancellationToken.None);
        rooms.HasActiveRooms = true;
        Result<PropertyMutationReceiptDto> replay = await handler.HandleAsync(
            command,
            CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.True(replay.IsSuccess);
        Assert.Equal(first.Value, replay.Value);
        Assert.Equal(1, rooms.ActiveRoomCheckCount);
        Assert.Single(operations.Added);
    }

    [Fact]
    public async Task Changed_retirement_reuse_conflicts_without_rechecking_rooms()
    {
        Property property = CreateProperty();
        FakeRoomRepository rooms = new();
        RecordingPropertyMutationOperationRepository operations = new();
        ServiceProvider provider = CreateProvider(property, rooms, operations);
        ICommandHandler<RetirePropertyCommand, PropertyMutationReceiptDto> handler =
            provider.GetRequiredService<ICommandHandler<RetirePropertyCommand, PropertyMutationReceiptDto>>();
        Guid operationId = Guid.NewGuid();

        Assert.True((await handler.HandleAsync(
            new RetirePropertyCommand(property.Id, operationId, true, 1),
            CancellationToken.None)).IsSuccess);
        Result<PropertyMutationReceiptDto> reuse = await handler.HandleAsync(
            new RetirePropertyCommand(property.Id, operationId, true, 2),
            CancellationToken.None);

        Assert.Equal(
            PropertiesApplicationErrors.ManagementOperationConflict,
            reuse.Error);
        Assert.Equal(1, rooms.ActiveRoomCheckCount);
        Assert.Single(operations.Added);
    }

    [Fact]
    public async Task Failed_active_room_check_does_not_bind_the_operation()
    {
        Property property = CreateProperty();
        FakeRoomRepository rooms = new() { HasActiveRooms = true };
        RecordingPropertyMutationOperationRepository operations = new();
        ServiceProvider provider = CreateProvider(property, rooms, operations);
        ICommandHandler<RetirePropertyCommand, PropertyMutationReceiptDto> handler =
            provider.GetRequiredService<ICommandHandler<RetirePropertyCommand, PropertyMutationReceiptDto>>();
        RetirePropertyCommand command = new(
            property.Id,
            Guid.NewGuid(),
            true,
            property.Version);

        Result<PropertyMutationReceiptDto> blocked = await handler.HandleAsync(
            command,
            CancellationToken.None);
        rooms.HasActiveRooms = false;
        Result<PropertyMutationReceiptDto> retry = await handler.HandleAsync(
            command,
            CancellationToken.None);

        Assert.Equal(PropertiesDomainErrors.PropertyHasActiveRooms, blocked.Error);
        Assert.True(retry.IsSuccess);
        Assert.Equal(2, rooms.ActiveRoomCheckCount);
        Assert.Single(operations.Added);
    }

    [Fact]
    public async Task Stale_retirement_is_rejected_before_active_rooms_are_queried()
    {
        Property property = CreateProperty();
        FakeRoomRepository rooms = new();
        RecordingPropertyMutationOperationRepository operations = new();
        ServiceProvider provider = CreateProvider(property, rooms, operations);
        ICommandHandler<RetirePropertyCommand, PropertyMutationReceiptDto> handler =
            provider.GetRequiredService<ICommandHandler<RetirePropertyCommand, PropertyMutationReceiptDto>>();

        Result<PropertyMutationReceiptDto> result = await handler.HandleAsync(
            new RetirePropertyCommand(
                property.Id,
                Guid.NewGuid(),
                true,
                ExpectedVersion: 99),
            CancellationToken.None);

        Assert.Equal(PropertiesDomainErrors.VersionConflict, result.Error);
        Assert.Equal(0, rooms.ActiveRoomCheckCount);
        Assert.Empty(operations.Added);
    }

    [Fact]
    public async Task Room_creation_uses_property_version_as_a_precondition()
    {
        Property property = CreateProperty();
        FakeRoomRepository rooms = new();
        ServiceProvider provider = CreateProvider(property, rooms);
        ICommandHandler<CreateRoomCommand, RoomMutationReceiptDto> handler =
            provider.GetRequiredService<ICommandHandler<CreateRoomCommand, RoomMutationReceiptDto>>();

        Result<RoomMutationReceiptDto> result = await handler.HandleAsync(
            new CreateRoomCommand(
                Guid.NewGuid(),
                property.Id,
                ExpectedPropertyVersion: 99,
                "101",
                null,
                null),
            CancellationToken.None);

        Assert.Equal(PropertiesDomainErrors.VersionConflict, result.Error);
        Assert.Null(rooms.AddedRoom);
        Assert.Equal(1, property.Version);
    }

    private static ServiceProvider CreateProvider(
        Property property,
        FakeRoomRepository rooms,
        RecordingPropertyMutationOperationRepository? operations = null)
    {
        ServiceCollection services = new();
        services.AddSingleton<IPropertyRepository>(new FakePropertyRepository(property));
        services.AddSingleton<IPropertyMutationOperationRepository>(
            operations ?? new RecordingPropertyMutationOperationRepository());
        services.AddSingleton<IRoomRepository>(rooms);
        services.AddSingleton<IScopeContext>(new TestScopeContext());
        services.AddSingleton<ISystemClock>(new TestClock());
        services.AddSingleton<IIdGenerator>(new TestIdGenerator());
        PropertiesMutationTestSupport.AddServiceDependencies(services);
        services.AddPropertiesApplication();
        return services.BuildServiceProvider();
    }

    private static Property CreateProperty() =>
        Property.Create(
            Guid.NewGuid(),
            "tenant-a",
            "Hostel One",
            "hostel-one",
            "UTC",
            Guid.NewGuid(),
            DateTimeOffset.UtcNow).Value;

    private sealed class FakePropertyRepository(Property property) : IPropertyRepository
    {
        public Task AddAsync(Property value, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<Property?> GetAsync(Guid propertyId, CancellationToken cancellationToken) =>
            Task.FromResult(property.Id == propertyId ? property : null);

        public Task<bool> CodeExistsAsync(string code, Guid? excludingPropertyId, CancellationToken cancellationToken) =>
            Task.FromResult(false);
    }

    private sealed class FakeRoomRepository : IRoomRepository
    {
        public bool HasActiveRooms { get; set; }
        public int ActiveRoomCheckCount { get; private set; }
        public Room? AddedRoom { get; private set; }

        public Task AddAsync(Room room, CancellationToken cancellationToken)
        {
            this.AddedRoom = room;
            return Task.CompletedTask;
        }

        public Task<Room?> GetAsync(Guid roomId, CancellationToken cancellationToken) =>
            Task.FromResult<Room?>(null);

        public Task<bool> HasActiveRoomsAsync(Guid propertyId, CancellationToken cancellationToken)
        {
            this.ActiveRoomCheckCount++;
            return Task.FromResult(this.HasActiveRooms);
        }

        public Task<bool> RoomNameExistsAsync(
            Guid propertyId,
            string name,
            Guid? excludingRoomId,
            CancellationToken cancellationToken) =>
            Task.FromResult(false);
    }

    private sealed class TestScopeContext : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => "tenant-a";
    }

    private sealed class TestClock : ISystemClock
    {
        public DateTimeOffset UtcNow { get; } = new(2026, 7, 10, 12, 0, 0, TimeSpan.Zero);
    }

    private sealed class TestIdGenerator : IIdGenerator
    {
        public Guid NewId() => Guid.CreateVersion7();
    }
}

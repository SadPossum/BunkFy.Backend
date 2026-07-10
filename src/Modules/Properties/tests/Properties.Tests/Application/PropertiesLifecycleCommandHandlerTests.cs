namespace Properties.Tests;

using Properties.Application;
using Properties.Application.Commands;
using Properties.Application.Ports;
using Properties.Contracts;
using Properties.Domain.Aggregates;
using Properties.Domain.Errors;
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
        ICommandHandler<RetirePropertyCommand, Unit> handler =
            provider.GetRequiredService<ICommandHandler<RetirePropertyCommand, Unit>>();

        Result<Unit> result = await handler.HandleAsync(
            new RetirePropertyCommand(property.Id, property.Version),
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
        ICommandHandler<RetirePropertyCommand, Unit> handler =
            provider.GetRequiredService<ICommandHandler<RetirePropertyCommand, Unit>>();

        Result<Unit> result = await handler.HandleAsync(
            new RetirePropertyCommand(property.Id, property.Version),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(PropertyState.Retired, property.Status);
        Assert.Equal(2, property.Version);
    }

    [Fact]
    public async Task Room_creation_uses_property_version_as_a_precondition()
    {
        Property property = CreateProperty();
        FakeRoomRepository rooms = new();
        ServiceProvider provider = CreateProvider(property, rooms);
        ICommandHandler<CreateRoomCommand, RoomDto> handler =
            provider.GetRequiredService<ICommandHandler<CreateRoomCommand, RoomDto>>();

        Result<RoomDto> result = await handler.HandleAsync(
            new CreateRoomCommand(property.Id, ExpectedPropertyVersion: 99, "101", null, null),
            CancellationToken.None);

        Assert.Equal(PropertiesDomainErrors.VersionConflict, result.Error);
        Assert.Null(rooms.AddedRoom);
        Assert.Equal(1, property.Version);
    }

    private static ServiceProvider CreateProvider(Property property, FakeRoomRepository rooms)
    {
        ServiceCollection services = new();
        services.AddSingleton<IPropertyRepository>(new FakePropertyRepository(property));
        services.AddSingleton<IRoomRepository>(rooms);
        services.AddSingleton<IScopeContext>(new TestScopeContext());
        services.AddSingleton<ISystemClock>(new TestClock());
        services.AddSingleton<IIdGenerator>(new TestIdGenerator());
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
            Task.FromResult<Property?>(property.Id == propertyId ? property : null);

        public Task<bool> CodeExistsAsync(string code, Guid? excludingPropertyId, CancellationToken cancellationToken) =>
            Task.FromResult(false);
    }

    private sealed class FakeRoomRepository : IRoomRepository
    {
        public bool HasActiveRooms { get; init; }
        public Room? AddedRoom { get; private set; }

        public Task AddAsync(Room room, CancellationToken cancellationToken)
        {
            this.AddedRoom = room;
            return Task.CompletedTask;
        }

        public Task<Room?> GetAsync(Guid roomId, CancellationToken cancellationToken) =>
            Task.FromResult<Room?>(null);

        public Task<bool> HasActiveRoomsAsync(Guid propertyId, CancellationToken cancellationToken) =>
            Task.FromResult(this.HasActiveRooms);

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

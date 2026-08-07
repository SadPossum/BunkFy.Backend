namespace BunkFy.Modules.Properties.Tests;

using BunkFy.Modules.Properties.Application.Commands;
using BunkFy.Modules.Properties.Application.Handlers;
using BunkFy.Modules.Properties.Application.Ports;
using BunkFy.Modules.Properties.Application;
using BunkFy.Modules.Properties.Contracts;
using BunkFy.Modules.Properties.Domain.Aggregates;
using BunkFy.Modules.Properties.Domain.Errors;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;
using Xunit;

[Trait("Category", "Unit")]
public sealed class RoomMutationCommandHandlerTests
{
    [Fact]
    public async Task Exact_room_creation_replay_returns_the_generated_receipt_once()
    {
        Property property = CreateProperty();
        RecordingRoomRepository rooms = new();
        RecordingPropertyMutationOperationRepository operations = new();
        CreateRoomCommandHandler handler = CreateRoomHandler(
            property,
            rooms,
            operations);
        Guid operationId = Guid.NewGuid();

        Result<RoomMutationReceiptDto> first = await handler.HandleAsync(
            new CreateRoomCommand(
                operationId,
                property.Id,
                property.Version,
                "  101  ",
                " Main ",
                null),
            CancellationToken.None);
        Result<RoomMutationReceiptDto> replay = await handler.HandleAsync(
            new CreateRoomCommand(
                operationId,
                property.Id,
                ExpectedPropertyVersion: 1,
                "101",
                "Main",
                null),
            CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.True(replay.IsSuccess);
        Assert.Equal(first.Value, replay.Value);
        Assert.Equal(2, property.Version);
        Assert.Equal(1, rooms.AddCount);
        PropertyMutationOperationRecord operation = Assert.Single(
            operations.Added);
        Assert.Equal(PropertyMutationResourceKind.Property, operation.ResourceKind);
        Assert.Equal(property.Id, operation.ResourceId);
        Assert.Equal(PropertyMutationKind.RoomCreate, operation.Kind);
        Assert.Equal(2, operation.ResultResourceVersion);
    }

    [Fact]
    public async Task Changed_room_creation_reuse_conflicts_before_current_version_checks()
    {
        Property property = CreateProperty();
        RecordingRoomRepository rooms = new();
        RecordingPropertyMutationOperationRepository operations = new();
        CreateRoomCommandHandler handler = CreateRoomHandler(
            property,
            rooms,
            operations);
        Guid operationId = Guid.NewGuid();

        Result<RoomMutationReceiptDto> first = await handler.HandleAsync(
            new CreateRoomCommand(
                operationId,
                property.Id,
                property.Version,
                "101",
                null,
                null),
            CancellationToken.None);
        Result<RoomMutationReceiptDto> changed = await handler.HandleAsync(
            new CreateRoomCommand(
                operationId,
                property.Id,
                ExpectedPropertyVersion: 1,
                "102",
                null,
                null),
            CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.Equal(
            PropertiesApplicationErrors.ManagementOperationConflict,
            changed.Error);
        Assert.Equal(1, rooms.AddCount);
    }

    [Fact]
    public async Task Failed_room_creation_does_not_bind_the_operation_id()
    {
        Property property = CreateProperty();
        RecordingRoomRepository rooms = new(CreateRoom(property.Id, "101"));
        RecordingPropertyMutationOperationRepository operations = new();
        CreateRoomCommandHandler handler = CreateRoomHandler(
            property,
            rooms,
            operations);
        Guid operationId = Guid.NewGuid();

        Result<RoomMutationReceiptDto> duplicate = await handler.HandleAsync(
            new CreateRoomCommand(
                operationId,
                property.Id,
                property.Version,
                "101",
                null,
                null),
            CancellationToken.None);
        Result<RoomMutationReceiptDto> corrected = await handler.HandleAsync(
            new CreateRoomCommand(
                operationId,
                property.Id,
                property.Version,
                "102",
                null,
                null),
            CancellationToken.None);

        Assert.Equal(PropertiesDomainErrors.RoomAlreadyExists, duplicate.Error);
        Assert.True(corrected.IsSuccess);
        Assert.Single(operations.Added);
        Assert.Equal(1, rooms.AddCount);
    }

    [Fact]
    public async Task Exact_room_update_replay_is_immutable_after_a_later_update()
    {
        Room room = CreateRoom(Guid.NewGuid(), "4A");
        room.ClearDomainEvents();
        RecordingRoomRepository rooms = new(room);
        RecordingPropertyMutationOperationRepository operations = new();
        UpdateRoomCommandHandler handler = CreateUpdateHandler(rooms, operations);
        Guid operationId = Guid.NewGuid();

        Result<RoomMutationReceiptDto> first = await handler.HandleAsync(
            new UpdateRoomCommand(
                operationId,
                room.PropertyId,
                room.Id,
                room.Version,
                " 4B ",
                " Main ",
                null),
            CancellationToken.None);
        Assert.True(first.IsSuccess);
        Assert.True(room.Update(
            "4C",
            null,
            null,
            room.Version,
            Guid.NewGuid(),
            Now.AddMinutes(1)).IsSuccess);
        room.ClearDomainEvents();

        Result<RoomMutationReceiptDto> replay = await handler.HandleAsync(
            new UpdateRoomCommand(
                operationId,
                room.PropertyId,
                room.Id,
                ExpectedVersion: 1,
                "4B",
                "Main",
                null),
            CancellationToken.None);

        Assert.True(replay.IsSuccess);
        Assert.Equal(first.Value, replay.Value);
        Assert.Equal(2, replay.Value.Version);
        Assert.Equal(3, room.Version);
        Assert.Empty(room.DomainEvents);
        Assert.Single(operations.Added);
    }

    [Fact]
    public async Task Unchanged_room_update_records_a_receipt_without_a_version_or_event()
    {
        Room room = CreateRoom(Guid.NewGuid(), "4A", "Main", "2");
        room.ClearDomainEvents();
        RecordingRoomRepository rooms = new(room);
        RecordingPropertyMutationOperationRepository operations = new();
        UpdateRoomCommandHandler handler = CreateUpdateHandler(rooms, operations);

        Result<RoomMutationReceiptDto> result = await handler.HandleAsync(
            new UpdateRoomCommand(
                Guid.NewGuid(),
                room.PropertyId,
                room.Id,
                room.Version,
                " 4A ",
                "Main",
                "2"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, room.Version);
        Assert.Empty(room.DomainEvents);
        Assert.Equal(0, rooms.NameCheckCount);
        PropertyMutationOperationRecord operation = Assert.Single(
            operations.Added);
        Assert.Equal(1, operation.ResultResourceVersion);
    }

    [Fact]
    public async Task Building_only_room_update_skips_the_name_uniqueness_check()
    {
        Room room = CreateRoom(Guid.NewGuid(), "4A", "Main", "2");
        room.ClearDomainEvents();
        RecordingRoomRepository rooms = new(room);
        RecordingPropertyMutationOperationRepository operations = new();
        UpdateRoomCommandHandler handler = CreateUpdateHandler(rooms, operations);

        Result<RoomMutationReceiptDto> result = await handler.HandleAsync(
            new UpdateRoomCommand(
                Guid.NewGuid(),
                room.PropertyId,
                room.Id,
                room.Version,
                "4A",
                "Annex",
                "2"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Annex", room.BuildingLabel?.Value);
        Assert.Equal(2, room.Version);
        Assert.Single(room.DomainEvents);
        Assert.Equal(0, rooms.NameCheckCount);
        Assert.Single(operations.Added);
    }

    [Fact]
    public async Task Changed_room_update_reuse_conflicts()
    {
        Room room = CreateRoom(Guid.NewGuid(), "4A");
        room.ClearDomainEvents();
        RecordingRoomRepository rooms = new(room);
        RecordingPropertyMutationOperationRepository operations = new();
        UpdateRoomCommandHandler handler = CreateUpdateHandler(rooms, operations);
        Guid operationId = Guid.NewGuid();

        Result<RoomMutationReceiptDto> first = await handler.HandleAsync(
            new UpdateRoomCommand(
                operationId,
                room.PropertyId,
                room.Id,
                room.Version,
                "4B",
                null,
                null),
            CancellationToken.None);
        Result<RoomMutationReceiptDto> changed = await handler.HandleAsync(
            new UpdateRoomCommand(
                operationId,
                room.PropertyId,
                room.Id,
                ExpectedVersion: 1,
                "4C",
                null,
                null),
            CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.Equal(
            PropertiesApplicationErrors.ManagementOperationConflict,
            changed.Error);
        Assert.Equal(2, room.Version);
        Assert.Single(operations.Added);
    }

    [Fact]
    public async Task Failed_room_update_does_not_bind_the_operation_id()
    {
        Guid propertyId = Guid.NewGuid();
        Room room = CreateRoom(propertyId, "4A");
        Room conflicting = CreateRoom(propertyId, "4B");
        room.ClearDomainEvents();
        RecordingRoomRepository rooms = new(room, conflicting);
        RecordingPropertyMutationOperationRepository operations = new();
        UpdateRoomCommandHandler handler = CreateUpdateHandler(rooms, operations);
        Guid operationId = Guid.NewGuid();

        Result<RoomMutationReceiptDto> duplicate = await handler.HandleAsync(
            new UpdateRoomCommand(
                operationId,
                propertyId,
                room.Id,
                room.Version,
                "4B",
                null,
                null),
            CancellationToken.None);
        Result<RoomMutationReceiptDto> corrected = await handler.HandleAsync(
            new UpdateRoomCommand(
                operationId,
                propertyId,
                room.Id,
                room.Version,
                "4C",
                null,
                null),
            CancellationToken.None);

        Assert.Equal(PropertiesDomainErrors.RoomAlreadyExists, duplicate.Error);
        Assert.True(corrected.IsSuccess);
        Assert.Equal("4C", room.Name.Value);
        Assert.Single(operations.Added);
    }

    [Fact]
    public async Task The_same_operation_id_is_isolated_between_room_resources()
    {
        Guid propertyId = Guid.NewGuid();
        Room firstRoom = CreateRoom(propertyId, "4A");
        Room secondRoom = CreateRoom(propertyId, "4B");
        firstRoom.ClearDomainEvents();
        secondRoom.ClearDomainEvents();
        RecordingRoomRepository rooms = new(firstRoom, secondRoom);
        RecordingPropertyMutationOperationRepository operations = new();
        UpdateRoomCommandHandler handler = CreateUpdateHandler(rooms, operations);
        Guid operationId = Guid.NewGuid();

        Result<RoomMutationReceiptDto> first = await handler.HandleAsync(
            new UpdateRoomCommand(
                operationId,
                propertyId,
                firstRoom.Id,
                firstRoom.Version,
                "4A East",
                null,
                null),
            CancellationToken.None);
        Result<RoomMutationReceiptDto> second = await handler.HandleAsync(
            new UpdateRoomCommand(
                operationId,
                propertyId,
                secondRoom.Id,
                secondRoom.Version,
                "4B West",
                null,
                null),
            CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(2, operations.Added.Count);
        Assert.All(
            operations.Added,
            operation => Assert.Equal(
                PropertyMutationResourceKind.Room,
                operation.ResourceKind));
    }

    private static CreateRoomCommandHandler CreateRoomHandler(
        Property property,
        RecordingRoomRepository rooms,
        RecordingPropertyMutationOperationRepository operations)
    {
        RecordingPropertyRepository properties = new(property);
        TestScopeContext scope = new();
        return new(
            rooms,
            PropertiesMutationTestSupport.Create(
                properties,
                rooms,
                scopeContext: scope),
            new PropertyMutationOperationJournal(operations),
            scope,
            new TestClock(),
            new TestIdGenerator());
    }

    private static UpdateRoomCommandHandler CreateUpdateHandler(
        RecordingRoomRepository rooms,
        RecordingPropertyMutationOperationRepository operations)
    {
        TestScopeContext scope = new();
        return new(
            rooms,
            PropertiesMutationTestSupport.Create(
                rooms: rooms,
                scopeContext: scope),
            new PropertyMutationOperationJournal(operations),
            scope,
            new TestClock(),
            new TestIdGenerator());
    }

    private static Property CreateProperty() => Property.Create(
        Guid.NewGuid(),
        TestScopeContext.TenantId,
        "Like Hostel",
        "LIKE",
        "UTC",
        Guid.NewGuid(),
        Now).Value;

    private static Room CreateRoom(
        Guid propertyId,
        string name,
        string? building = null,
        string? floor = null) => Room.Create(
            Guid.NewGuid(),
            TestScopeContext.TenantId,
            propertyId,
            name,
            building,
            floor,
            Guid.NewGuid(),
            Now).Value;

    private sealed class RecordingPropertyRepository(Property property)
        : IPropertyRepository
    {
        public Task AddAsync(
            Property candidate,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Property?> GetAsync(
            Guid propertyId,
            CancellationToken cancellationToken) => Task.FromResult(
                property.Id == propertyId ? property : null);

        public Task<bool> CodeExistsAsync(
            string code,
            Guid? excludingPropertyId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingRoomRepository(params Room[] initial)
        : IRoomRepository
    {
        private readonly List<Room> rooms = [.. initial];

        public int AddCount { get; private set; }
        public int NameCheckCount { get; private set; }

        public Task AddAsync(
            Room room,
            CancellationToken cancellationToken)
        {
            this.AddCount++;
            this.rooms.Add(room);
            return Task.CompletedTask;
        }

        public Task<Room?> GetAsync(
            Guid roomId,
            CancellationToken cancellationToken) => Task.FromResult(
                this.rooms.SingleOrDefault(room => room.Id == roomId));

        public Task<bool> HasActiveRoomsAsync(
            Guid propertyId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> RoomNameExistsAsync(
            Guid propertyId,
            string name,
            Guid? excludingRoomId,
            CancellationToken cancellationToken)
        {
            this.NameCheckCount++;
            return Task.FromResult(this.rooms.Any(room =>
                room.PropertyId == propertyId &&
                room.Id != excludingRoomId &&
                string.Equals(room.Name.Value, name, StringComparison.Ordinal)));
        }
    }

    private sealed class TestScopeContext : IScopeContext
    {
        public const string TenantId = "tenant-a";
        public bool IsEnabled => true;
        public string? ScopeId => TenantId;
    }

    private sealed class TestClock : ISystemClock
    {
        public DateTimeOffset UtcNow => Now;
    }

    private sealed class TestIdGenerator : IIdGenerator
    {
        public Guid NewId() => Guid.NewGuid();
    }

    private static readonly DateTimeOffset Now = new(
        2026,
        8,
        7,
        20,
        0,
        0,
        TimeSpan.Zero);
}

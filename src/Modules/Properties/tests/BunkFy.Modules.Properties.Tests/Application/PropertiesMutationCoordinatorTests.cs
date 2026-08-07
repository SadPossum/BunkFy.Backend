namespace BunkFy.Modules.Properties.Tests;

using System.Reflection;
using BunkFy.Modules.Properties.Application.Commands;
using BunkFy.Modules.Properties.Application.Handlers;
using BunkFy.Modules.Properties.Application.Ports;
using BunkFy.Modules.Properties.Contracts;
using BunkFy.Modules.Properties.Domain.Aggregates;
using BunkFy.Modules.Properties.Domain.Errors;
using BunkFy.Modules.Properties.Domain.ValueObjects;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;
using Xunit;

[Trait("Category", "Unit")]
public sealed class PropertiesMutationCoordinatorTests
{
    [Fact]
    public async Task Property_lock_precedes_the_authoritative_reload()
    {
        Property property = CreateProperty();
        List<string> calls = [];
        SequencedPropertyRepository properties = new(property, calls);
        PropertiesMutationCoordinator coordinator = CreateCoordinator(
            properties,
            new EmptyRoomRepository(),
            new CallbackOperationLock(
                calls,
                propertyAcquired: () => properties.IsVisible = false));

        Property? result = await coordinator.AcquirePropertyAsync(
            property.Id,
            CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(["property-lock", "property-reload"], calls);
    }

    [Fact]
    public async Task Room_lock_precedes_the_authoritative_reload()
    {
        Room room = CreateRoom();
        List<string> calls = [];
        SequencedRoomRepository rooms = new(room, calls);
        PropertiesMutationCoordinator coordinator = CreateCoordinator(
            new EmptyPropertyRepository(),
            rooms,
            new CallbackOperationLock(
                calls,
                roomAcquired: () => rooms.IsVisible = false));

        Room? result = await coordinator.AcquireRoomAsync(
            room.Id,
            CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(["room-lock", "room-reload"], calls);
    }

    [Fact]
    public async Task Missing_lock_skips_the_authoritative_reload()
    {
        List<string> calls = [];
        PropertiesMutationCoordinator coordinator = CreateCoordinator(
            new SequencedPropertyRepository(CreateProperty(), calls),
            new EmptyRoomRepository(),
            new CallbackOperationLock(
                calls,
                propertyExists: false));

        Property? result = await coordinator.AcquirePropertyAsync(
            Guid.NewGuid(),
            CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(["property-lock"], calls);
    }

    [Fact]
    public async Task Unique_coordinates_use_normalized_domain_values()
    {
        RecordingUniqueCoordinateLock uniqueCoordinates = new();
        PropertiesMutationCoordinator coordinator = new(
            new EmptyPropertyRepository(),
            new EmptyRoomRepository(),
            new CallbackOperationLock([]),
            uniqueCoordinates,
            new TestScopeContext());
        Guid propertyId = Guid.NewGuid();

        await coordinator.AcquirePropertyCodeAsync(
            PropertyCode.Create("  Hostel-One  ").Value,
            CancellationToken.None);
        await coordinator.AcquireRoomNameAsync(
            propertyId,
            RoomName.Create("  4A  ").Value,
            CancellationToken.None);

        Assert.Equal(
            ("tenant-a", "hostel-one"),
            uniqueCoordinates.PropertyCode);
        Assert.Equal(
            ("tenant-a", propertyId, "4A"),
            uniqueCoordinates.RoomName);
    }

    [Fact]
    public async Task Contended_room_update_returns_the_domain_version_conflict()
    {
        Room room = CreateRoom();
        long submittedVersion = room.Version;
        List<string> calls = [];
        SequencedRoomRepository rooms = new(room, calls);
        CallbackOperationLock operationLock = new(
            calls,
            roomAcquired: () =>
            {
                Result concurrent = room.Update(
                    "Concurrent 4A",
                    room.BuildingLabel?.Value,
                    room.FloorLabel?.Value,
                    submittedVersion,
                    Guid.NewGuid(),
                    new DateTimeOffset(
                        2026,
                        8,
                        6,
                        9,
                        1,
                        0,
                        TimeSpan.Zero));
                Assert.True(concurrent.IsSuccess);
            });
        PropertiesMutationCoordinator coordinator = CreateCoordinator(
            new EmptyPropertyRepository(),
            rooms,
            operationLock);
        UpdateRoomCommandHandler handler = new(
            rooms,
            coordinator,
            new PropertyMutationOperationJournal(
                new RecordingPropertyMutationOperationRepository()),
            new TestScopeContext(),
            new TestClock(),
            new TestIdGenerator());

        Result<RoomMutationReceiptDto> result = await handler.HandleAsync(
            new UpdateRoomCommand(
                Guid.NewGuid(),
                room.PropertyId,
                room.Id,
                submittedVersion,
                "Operator 4A",
                null,
                null),
            CancellationToken.None);

        Assert.Equal(PropertiesDomainErrors.VersionConflict, result.Error);
        Assert.Equal("Concurrent 4A", room.Name.Value);
        Assert.Equal(["room-lock", "room-reload"], calls);
    }

    [Theory]
    [InlineData(typeof(CreatePropertyCommandHandler))]
    [InlineData(typeof(UpdatePropertyCommandHandler))]
    [InlineData(typeof(ActivatePropertyProcessingCommandHandler))]
    [InlineData(typeof(SuspendPropertyProcessingCommandHandler))]
    [InlineData(typeof(RetirePropertyCommandHandler))]
    [InlineData(typeof(CreateRoomCommandHandler))]
    [InlineData(typeof(UpdateRoomCommandHandler))]
    [InlineData(typeof(AddBedCommandHandler))]
    [InlineData(typeof(AddBedsCommandHandler))]
    [InlineData(typeof(UpdateBedCommandHandler))]
    [InlineData(typeof(BedRetirementFinalizationRequestedHandler))]
    [InlineData(typeof(RoomRetirementFinalizationRequestedHandler))]
    public void Mutation_paths_require_the_coordinator(Type handlerType)
    {
        bool hasCoordinator = handlerType
            .GetConstructors(
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic)
            .SelectMany(constructor => constructor.GetParameters())
            .Any(parameter => parameter.ParameterType ==
                typeof(PropertiesMutationCoordinator));

        Assert.True(
            hasCoordinator,
            $"{handlerType.Name} must serialize through " +
            $"{nameof(PropertiesMutationCoordinator)}.");
    }

    private static PropertiesMutationCoordinator CreateCoordinator(
        IPropertyRepository properties,
        IRoomRepository rooms,
        IPropertiesOperationLock operationLock) => new(
            properties,
            rooms,
            operationLock,
            new RecordingUniqueCoordinateLock(),
            new TestScopeContext());

    private static Property CreateProperty() => Property.Create(
        Guid.NewGuid(),
        "tenant-a",
        "Hostel One",
        "hostel-one",
        "UTC",
        Guid.NewGuid(),
        new DateTimeOffset(2026, 8, 6, 9, 0, 0, TimeSpan.Zero)).Value;

    private static Room CreateRoom() => Room.Create(
        Guid.NewGuid(),
        "tenant-a",
        Guid.NewGuid(),
        "4A",
        null,
        null,
        Guid.NewGuid(),
        new DateTimeOffset(2026, 8, 6, 9, 0, 0, TimeSpan.Zero)).Value;

    private sealed class CallbackOperationLock(
        List<string> calls,
        Action? propertyAcquired = null,
        Action? roomAcquired = null,
        bool propertyExists = true,
        bool roomExists = true)
        : IPropertiesOperationLock
    {
        public Task<bool> TryAcquirePropertyAsync(
            string tenantId,
            Guid propertyId,
            CancellationToken cancellationToken)
        {
            Assert.Equal("tenant-a", tenantId);
            calls.Add("property-lock");
            propertyAcquired?.Invoke();
            return Task.FromResult(propertyExists);
        }

        public Task<bool> TryAcquireRoomAsync(
            string tenantId,
            Guid roomId,
            CancellationToken cancellationToken)
        {
            Assert.Equal("tenant-a", tenantId);
            calls.Add("room-lock");
            roomAcquired?.Invoke();
            return Task.FromResult(roomExists);
        }
    }

    private sealed class RecordingUniqueCoordinateLock
        : IPropertiesUniqueCoordinateLock
    {
        public (string TenantId, string Code)? PropertyCode { get; private set; }
        public (string TenantId, Guid PropertyId, string Name)? RoomName
        {
            get;
            private set;
        }

        public Task AcquirePropertyCodeAsync(
            string tenantId,
            string propertyCode,
            CancellationToken cancellationToken)
        {
            this.PropertyCode = (tenantId, propertyCode);
            return Task.CompletedTask;
        }

        public Task AcquireRoomNameAsync(
            string tenantId,
            Guid propertyId,
            string roomName,
            CancellationToken cancellationToken)
        {
            this.RoomName = (tenantId, propertyId, roomName);
            return Task.CompletedTask;
        }
    }

    private sealed class SequencedPropertyRepository(
        Property property,
        List<string> calls)
        : IPropertyRepository
    {
        public bool IsVisible { get; set; } = true;

        public Task AddAsync(
            Property value,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Property?> GetAsync(
            Guid propertyId,
            CancellationToken cancellationToken)
        {
            calls.Add("property-reload");
            return Task.FromResult<Property?>(
                this.IsVisible && property.Id == propertyId
                    ? property
                    : null);
        }

        public Task<bool> CodeExistsAsync(
            string code,
            Guid? excludingPropertyId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class SequencedRoomRepository(
        Room room,
        List<string> calls)
        : IRoomRepository
    {
        public bool IsVisible { get; set; } = true;

        public Task AddAsync(
            Room value,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Room?> GetAsync(
            Guid roomId,
            CancellationToken cancellationToken)
        {
            calls.Add("room-reload");
            return Task.FromResult<Room?>(
                this.IsVisible && room.Id == roomId ? room : null);
        }

        public Task<bool> HasActiveRoomsAsync(
            Guid propertyId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> RoomNameExistsAsync(
            Guid propertyId,
            string name,
            Guid? excludingRoomId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class EmptyPropertyRepository : IPropertyRepository
    {
        public Task AddAsync(
            Property property,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Property?> GetAsync(
            Guid propertyId,
            CancellationToken cancellationToken) =>
            Task.FromResult<Property?>(null);

        public Task<bool> CodeExistsAsync(
            string code,
            Guid? excludingPropertyId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class EmptyRoomRepository : IRoomRepository
    {
        public Task AddAsync(
            Room room,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Room?> GetAsync(
            Guid roomId,
            CancellationToken cancellationToken) =>
            Task.FromResult<Room?>(null);

        public Task<bool> HasActiveRoomsAsync(
            Guid propertyId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> RoomNameExistsAsync(
            Guid propertyId,
            string name,
            Guid? excludingRoomId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class TestScopeContext : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => "tenant-a";
    }

    private sealed class TestClock : ISystemClock
    {
        public DateTimeOffset UtcNow =>
            new(2026, 8, 6, 9, 2, 0, TimeSpan.Zero);
    }

    private sealed class TestIdGenerator : IIdGenerator
    {
        public Guid NewId() => Guid.CreateVersion7();
    }
}

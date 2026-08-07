namespace BunkFy.Modules.Properties.Tests;

using BunkFy.Modules.Properties.Domain.Aggregates;
using BunkFy.Modules.Properties.Persistence;
using BunkFy.Modules.Properties.Persistence.Repositories;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;
using Xunit;

[Trait("Category", "Unit")]
public sealed class PropertiesOperationLockRepositoryTests
{
    [Fact]
    public async Task Repositories_provision_lock_rows_with_the_aggregate()
    {
        await using PropertiesDbContext dbContext = CreateDbContext();
        Property property = CreateProperty();
        Room room = CreateRoom(property.Id);
        PropertyRepository properties = new(dbContext);
        RoomRepository rooms = new(dbContext);

        await properties.AddAsync(property, CancellationToken.None);
        await rooms.AddAsync(room, CancellationToken.None);
        await dbContext.SaveChangesAsync();

        PropertyOperationLock propertyLock =
            await dbContext.PropertyOperationLocks.SingleAsync();
        RoomOperationLock roomLock =
            await dbContext.RoomOperationLocks.SingleAsync();
        Assert.Equal(property.Id, propertyLock.Id);
        Assert.Equal(property.Id, propertyLock.PropertyId);
        Assert.Equal(property.ScopeId, propertyLock.ScopeId);
        Assert.Equal(1, propertyLock.Revision);
        Assert.Equal(room.Id, roomLock.Id);
        Assert.Equal(room.Id, roomLock.RoomId);
        Assert.Equal(room.ScopeId, roomLock.ScopeId);
        Assert.Equal(1, roomLock.Revision);
    }

    [Fact]
    public async Task In_memory_fallback_advances_each_provisioned_lock()
    {
        await using PropertiesDbContext dbContext = CreateDbContext();
        Property property = CreateProperty();
        Room room = CreateRoom(property.Id);
        await new PropertyRepository(dbContext).AddAsync(
            property,
            CancellationToken.None);
        await new RoomRepository(dbContext).AddAsync(
            room,
            CancellationToken.None);
        await dbContext.SaveChangesAsync();
        PropertiesOperationLockRepository locks = new(dbContext);

        Assert.True(await locks.TryAcquirePropertyAsync(
            "tenant-a",
            property.Id,
            CancellationToken.None));
        Assert.True(await locks.TryAcquirePropertyAsync(
            "tenant-a",
            property.Id,
            CancellationToken.None));
        Assert.True(await locks.TryAcquireRoomAsync(
            "tenant-a",
            room.Id,
            CancellationToken.None));

        Assert.Equal(
            3,
            (await dbContext.PropertyOperationLocks.SingleAsync()).Revision);
        Assert.Equal(
            2,
            (await dbContext.RoomOperationLocks.SingleAsync()).Revision);
    }

    [Fact]
    public async Task Unknown_aggregate_returns_false_without_creating_a_lock()
    {
        await using PropertiesDbContext dbContext = CreateDbContext();
        PropertiesOperationLockRepository locks = new(dbContext);

        Assert.False(await locks.TryAcquirePropertyAsync(
            "tenant-a",
            Guid.NewGuid(),
            CancellationToken.None));
        Assert.False(await locks.TryAcquireRoomAsync(
            "tenant-a",
            Guid.NewGuid(),
            CancellationToken.None));
        Assert.Empty(dbContext.PropertyOperationLocks);
        Assert.Empty(dbContext.RoomOperationLocks);
    }

    [Fact]
    public async Task Existing_aggregate_without_a_lock_is_rejected_as_corrupt()
    {
        await using PropertiesDbContext dbContext = CreateDbContext();
        Property property = CreateProperty();
        Room room = CreateRoom(property.Id);
        dbContext.Properties.Add(property);
        dbContext.Rooms.Add(room);
        await dbContext.SaveChangesAsync();
        PropertiesOperationLockRepository locks = new(dbContext);

        InvalidOperationException propertyFailure =
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                locks.TryAcquirePropertyAsync(
                    "tenant-a",
                    property.Id,
                    CancellationToken.None));
        InvalidOperationException roomFailure =
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                locks.TryAcquireRoomAsync(
                    "tenant-a",
                    room.Id,
                    CancellationToken.None));

        Assert.Equal(
            "The Property operation lock is not provisioned.",
            propertyFailure.Message);
        Assert.Equal(
            "The Room operation lock is not provisioned.",
            roomFailure.Message);
    }

    [Fact]
    public async Task Cross_scope_coordinate_is_rejected_before_persistence()
    {
        await using PropertiesDbContext dbContext = CreateDbContext();
        PropertiesOperationLockRepository locks = new(dbContext);
        PropertiesCreationOperationLock creationLock = new(dbContext);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            locks.TryAcquirePropertyAsync(
                "tenant-b",
                Guid.NewGuid(),
                CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            locks.AcquireRoomNameAsync(
                "tenant-b",
                Guid.NewGuid(),
                "4A",
                CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            creationLock.AcquireAsync(
                "tenant-b",
                Guid.NewGuid(),
                CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            creationLock.AcquireAsync(
                "tenant-a",
                Guid.Empty,
                CancellationToken.None));

        Assert.Empty(dbContext.PropertyOperationLocks);
        Assert.Empty(dbContext.RoomOperationLocks);
    }

    [Fact]
    public async Task Valid_creation_coordinate_passes_in_memory_admission()
    {
        await using PropertiesDbContext dbContext = CreateDbContext();
        PropertiesCreationOperationLock creationLock = new(dbContext);

        await creationLock.AcquireAsync(
            "tenant-a",
            Guid.NewGuid(),
            CancellationToken.None);
    }

    private static PropertiesDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<PropertiesDbContext>()
            .UseInMemoryDatabase(
                $"properties-operation-lock-{Guid.NewGuid():N}")
            .Options,
        new TestScopeContext());

    private static Property CreateProperty() => Property.Create(
        Guid.NewGuid(),
        "tenant-a",
        "Hostel One",
        "hostel-one",
        "UTC",
        Guid.NewGuid(),
        new DateTimeOffset(2026, 8, 6, 9, 0, 0, TimeSpan.Zero)).Value;

    private static Room CreateRoom(Guid propertyId) => Room.Create(
        Guid.NewGuid(),
        "tenant-a",
        propertyId,
        "4A",
        null,
        null,
        Guid.NewGuid(),
        new DateTimeOffset(2026, 8, 6, 9, 0, 0, TimeSpan.Zero)).Value;

    private sealed class TestScopeContext : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => "tenant-a";
    }
}

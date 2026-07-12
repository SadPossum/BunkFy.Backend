namespace BunkFy.Modules.Properties.Tests;

using BunkFy.Modules.Properties.Domain.Aggregates;
using BunkFy.Modules.Properties.Domain.Entities;
using BunkFy.Modules.Properties.Domain.Errors;
using BunkFy.Modules.Properties.Domain.Events;
using Gma.Framework.Results;
using Xunit;

[Trait("Category", "Unit")]
public sealed class RoomAggregateTests
{
    [Fact]
    public void Create_normalizes_labels_and_raises_event()
    {
        Result<Room> result = CreateRoom(buildingLabel: " Main ", floorLabel: " 2 ");

        Assert.True(result.IsSuccess);
        Assert.Equal("Main", result.Value.BuildingLabel?.Value);
        Assert.Equal("2", result.Value.FloorLabel?.Value);
        Assert.IsType<RoomCreatedDomainEvent>(Assert.Single(result.Value.DomainEvents));
    }

    [Fact]
    public void Add_update_and_retire_bed_enforces_room_and_bed_lifecycle()
    {
        Room room = CreateRoom().Value;
        room.ClearDomainEvents();

        Result<Bed> added = room.AddBed(Guid.NewGuid(), " A ", room.Version, Guid.NewGuid(), DateTimeOffset.UtcNow);
        Result<Bed> duplicate = room.AddBed(Guid.NewGuid(), "A", room.Version, Guid.NewGuid(), DateTimeOffset.UtcNow);
        Result<Bed> updated = room.UpdateBed(added.Value.Id, "B", room.Version, Guid.NewGuid(), DateTimeOffset.UtcNow);
        Result retired = room.RetireBed(added.Value.Id, room.Version, Guid.NewGuid(), DateTimeOffset.UtcNow);
        Result<Bed> updateRetired = room.UpdateBed(added.Value.Id, "C", room.Version, Guid.NewGuid(), DateTimeOffset.UtcNow);

        Assert.True(added.IsSuccess);
        Assert.Equal(PropertiesDomainErrors.BedAlreadyExists, duplicate.Error);
        Assert.True(updated.IsSuccess);
        Assert.Equal("B", updated.Value.Label.Value);
        Assert.True(retired.IsSuccess);
        Assert.Equal(BedState.Retired, added.Value.Status);
        Assert.Equal(3, added.Value.Version);
        Assert.Equal(4, room.Version);
        Assert.Equal(PropertiesDomainErrors.BedAlreadyRetired, updateRetired.Error);
        Assert.Contains(room.DomainEvents, domainEvent => domainEvent is BedAddedDomainEvent);
        Assert.Contains(room.DomainEvents, domainEvent => domainEvent is BedUpdatedDomainEvent);
        Assert.Contains(room.DomainEvents, domainEvent => domainEvent is BedRetiredDomainEvent);
    }

    [Fact]
    public void Retired_room_rejects_mutations()
    {
        Room room = CreateRoom().Value;

        Assert.True(room.Retire(room.Version, false, [], Guid.NewGuid(), DateTimeOffset.UtcNow).IsSuccess);
        Assert.Equal(
            PropertiesDomainErrors.RoomRetired,
            room.Update("Updated", null, null, room.Version, Guid.NewGuid(), DateTimeOffset.UtcNow).Error);
        Assert.Equal(
            PropertiesDomainErrors.RoomRetired,
            room.AddBed(Guid.NewGuid(), "A", room.Version, Guid.NewGuid(), DateTimeOffset.UtcNow).Error);
    }

    [Fact]
    public void Room_retirement_requires_explicit_bed_cascade_and_orders_events()
    {
        Room room = CreateRoom().Value;
        room.AddBed(Guid.NewGuid(), "A", room.Version, Guid.NewGuid(), DateTimeOffset.UtcNow);
        room.AddBed(Guid.NewGuid(), "B", room.Version, Guid.NewGuid(), DateTimeOffset.UtcNow);
        room.ClearDomainEvents();
        long versionBeforeRetirement = room.Version;

        Result withoutCascade = room.Retire(
            room.Version,
            cascadeBeds: false,
            [],
            Guid.NewGuid(),
            DateTimeOffset.UtcNow);

        Assert.Equal(PropertiesDomainErrors.RoomHasActiveBeds, withoutCascade.Error);
        Assert.Equal(versionBeforeRetirement, room.Version);

        Result cascaded = room.Retire(
            room.Version,
            cascadeBeds: true,
            [Guid.NewGuid(), Guid.NewGuid()],
            Guid.NewGuid(),
            DateTimeOffset.UtcNow);

        Assert.True(cascaded.IsSuccess);
        Assert.Equal(RoomState.Retired, room.Status);
        Assert.All(room.Beds, bed => Assert.Equal(BedState.Retired, bed.Status));
        Assert.Collection(
            room.DomainEvents,
            domainEvent => Assert.IsType<BedRetiredDomainEvent>(domainEvent),
            domainEvent => Assert.IsType<BedRetiredDomainEvent>(domainEvent),
            domainEvent => Assert.IsType<RoomRetiredDomainEvent>(domainEvent));
    }

    [Fact]
    public void Stale_room_version_is_rejected_without_mutation()
    {
        Room room = CreateRoom().Value;

        Result result = room.Update("Updated", null, null, expectedVersion: 99, Guid.NewGuid(), DateTimeOffset.UtcNow);

        Assert.Equal(PropertiesDomainErrors.VersionConflict, result.Error);
        Assert.Equal(1, room.Version);
        Assert.Equal("101", room.Name.Value);
    }

    private static Result<Room> CreateRoom(
        string tenantId = "tenant-a",
        Guid? propertyId = null,
        string name = "101",
        string? buildingLabel = null,
        string? floorLabel = null) =>
        Room.Create(
            Guid.NewGuid(),
            tenantId,
            propertyId ?? Guid.NewGuid(),
            name,
            buildingLabel,
            floorLabel,
            Guid.NewGuid(),
            DateTimeOffset.UtcNow);
}

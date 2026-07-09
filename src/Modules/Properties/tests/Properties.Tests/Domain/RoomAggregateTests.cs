namespace Properties.Tests;

using Properties.Domain.Aggregates;
using Properties.Domain.Entities;
using Properties.Domain.Errors;
using Properties.Domain.Events;
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

        Result<Bed> added = room.AddBed(Guid.NewGuid(), " A ", Guid.NewGuid(), DateTimeOffset.UtcNow);
        Result<Bed> duplicate = room.AddBed(Guid.NewGuid(), "A", Guid.NewGuid(), DateTimeOffset.UtcNow);
        Result<Bed> updated = room.UpdateBed(added.Value.Id, "B", Guid.NewGuid(), DateTimeOffset.UtcNow);
        Result retired = room.RetireBed(added.Value.Id, Guid.NewGuid(), DateTimeOffset.UtcNow);
        Result<Bed> updateRetired = room.UpdateBed(added.Value.Id, "C", Guid.NewGuid(), DateTimeOffset.UtcNow);

        Assert.True(added.IsSuccess);
        Assert.Equal(PropertiesDomainErrors.BedAlreadyExists, duplicate.Error);
        Assert.True(updated.IsSuccess);
        Assert.Equal("B", updated.Value.Label.Value);
        Assert.True(retired.IsSuccess);
        Assert.Equal(BedState.Retired, added.Value.Status);
        Assert.Equal(PropertiesDomainErrors.BedAlreadyRetired, updateRetired.Error);
        Assert.Contains(room.DomainEvents, domainEvent => domainEvent is BedAddedDomainEvent);
        Assert.Contains(room.DomainEvents, domainEvent => domainEvent is BedUpdatedDomainEvent);
        Assert.Contains(room.DomainEvents, domainEvent => domainEvent is BedRetiredDomainEvent);
    }

    [Fact]
    public void Retired_room_rejects_mutations()
    {
        Room room = CreateRoom().Value;

        Assert.True(room.Retire(Guid.NewGuid(), DateTimeOffset.UtcNow).IsSuccess);
        Assert.Equal(PropertiesDomainErrors.RoomRetired, room.Update("Updated", null, null, Guid.NewGuid(), DateTimeOffset.UtcNow).Error);
        Assert.Equal(
            PropertiesDomainErrors.RoomRetired,
            room.AddBed(Guid.NewGuid(), "A", Guid.NewGuid(), DateTimeOffset.UtcNow).Error);
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

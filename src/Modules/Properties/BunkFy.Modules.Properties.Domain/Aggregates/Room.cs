namespace BunkFy.Modules.Properties.Domain.Aggregates;

using BunkFy.Modules.Properties.Domain.Entities;
using BunkFy.Modules.Properties.Domain.Errors;
using BunkFy.Modules.Properties.Domain.Events;
using BunkFy.Modules.Properties.Domain.ValueObjects;
using Gma.Framework.Domain.Models;
using Gma.Framework.Results;

public sealed partial class Room : ScopedAggregateRoot<Guid>
{
    public const int RoomNameMaxLength = 128;
    public const int PhysicalLabelMaxLength = 128;
    public const int BedLabelMaxLength = 128;

    private readonly List<Bed> beds = [];

    private Room() { }

    private Room(Guid id, string scopeId)
        : base(id, scopeId)
    {
    }

    public Guid PropertyId { get; private set; }
    public RoomName Name { get; private set; }
    public PhysicalLabel? BuildingLabel { get; private set; }
    public PhysicalLabel? FloorLabel { get; private set; }
    public RoomState Status { get; private set; } = RoomState.Active;
    public long Version { get; private set; } = 1;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? UpdatedAtUtc { get; private set; }
    public DateTimeOffset? RetiredAtUtc { get; private set; }
    public IReadOnlyCollection<Bed> Beds => this.beds.AsReadOnly();

    public static Result<Room> Create(
        Guid id,
        string tenantId,
        Guid propertyId,
        string name,
        string? buildingLabel,
        string? floorLabel,
        Guid eventId,
        DateTimeOffset nowUtc)
    {
        if (id == Guid.Empty)
        {
            return Result.Failure<Room>(PropertiesDomainErrors.RoomIdRequired);
        }

        if (propertyId == Guid.Empty)
        {
            return Result.Failure<Room>(PropertiesDomainErrors.PropertyIdRequired);
        }

        if (eventId == Guid.Empty)
        {
            return Result.Failure<Room>(PropertiesDomainErrors.DomainEventIdRequired);
        }

        Result<RoomDefinition> values = RoomDefinition.Create(tenantId, name, buildingLabel, floorLabel);
        if (values.IsFailure)
        {
            return Result.Failure<Room>(values.Error);
        }

        Room room = new(id, values.Value.ScopeId)
        {
            PropertyId = propertyId,
            Name = values.Value.Name,
            BuildingLabel = values.Value.BuildingLabel,
            FloorLabel = values.Value.FloorLabel,
            CreatedAtUtc = nowUtc
        };

        room.RaiseDomainEvent(new RoomCreatedDomainEvent(
            eventId,
            nowUtc,
            room.PropertyId,
            room.Id,
            room.ScopeId,
            room.Name.Value,
            room.BuildingLabel?.Value,
            room.FloorLabel?.Value,
            room.Status,
            room.Version));

        return Result.Success(room);
    }

    public Result Update(
        string name,
        string? buildingLabel,
        string? floorLabel,
        long expectedVersion,
        Guid eventId,
        DateTimeOffset nowUtc)
    {
        Result statusResult = this.EnsureActive();
        if (statusResult.IsFailure)
        {
            return statusResult;
        }

        Result versionResult = this.EnsureExpectedVersion(expectedVersion);
        if (versionResult.IsFailure)
        {
            return versionResult;
        }

        if (eventId == Guid.Empty)
        {
            return Result.Failure(PropertiesDomainErrors.DomainEventIdRequired);
        }

        Result<RoomDefinition> values = RoomDefinition.Create(this.ScopeId, name, buildingLabel, floorLabel);
        if (values.IsFailure)
        {
            return Result.Failure(values.Error);
        }

        this.Name = values.Value.Name;
        this.BuildingLabel = values.Value.BuildingLabel;
        this.FloorLabel = values.Value.FloorLabel;
        this.UpdatedAtUtc = nowUtc;
        this.Version++;

        this.RaiseDomainEvent(new RoomUpdatedDomainEvent(
            eventId,
            nowUtc,
            this.PropertyId,
            this.Id,
            this.ScopeId,
            this.Name.Value,
            this.BuildingLabel?.Value,
            this.FloorLabel?.Value,
            this.Status,
            this.Version));

        return Result.Success();
    }

    public Result Retire(
        long expectedVersion,
        bool cascadeBeds,
        IReadOnlyCollection<Guid> bedRetiredEventIds,
        Guid eventId,
        DateTimeOffset nowUtc)
    {
        Result statusResult = this.EnsureActive();
        if (statusResult.IsFailure)
        {
            return statusResult;
        }

        Result versionResult = this.EnsureExpectedVersion(expectedVersion);
        if (versionResult.IsFailure)
        {
            return versionResult;
        }

        if (eventId == Guid.Empty)
        {
            return Result.Failure(PropertiesDomainErrors.DomainEventIdRequired);
        }

        List<Bed> activeBeds = [.. this.beds.Where(bed => bed.Status == BedState.Active)];
        if (activeBeds.Count > 0 && !cascadeBeds)
        {
            return Result.Failure(PropertiesDomainErrors.RoomHasActiveBeds);
        }

        ArgumentNullException.ThrowIfNull(bedRetiredEventIds);
        Guid[] cascadeEventIds = [.. bedRetiredEventIds];
        if (cascadeEventIds.Length != activeBeds.Count ||
            cascadeEventIds.Any(id => id == Guid.Empty) ||
            cascadeEventIds.Distinct().Count() != cascadeEventIds.Length)
        {
            return Result.Failure(PropertiesDomainErrors.DomainEventIdRequired);
        }

        this.Status = RoomState.Retired;
        this.RetiredAtUtc = nowUtc;
        this.UpdatedAtUtc = nowUtc;
        this.Version++;

        for (int index = 0; index < activeBeds.Count; index++)
        {
            Bed bed = activeBeds[index];
            bed.Retire(nowUtc);
            this.RaiseDomainEvent(new BedRetiredDomainEvent(
                cascadeEventIds[index],
                nowUtc,
                this.PropertyId,
                this.Id,
                bed.Id,
                this.ScopeId,
                this.Version,
                bed.Version));
        }

        this.RaiseDomainEvent(new RoomRetiredDomainEvent(
            eventId,
            nowUtc,
            this.PropertyId,
            this.Id,
            this.ScopeId,
            this.Version));

        return Result.Success();
    }

    private bool HasBedLabel(BedLabel label, Guid? excludingBedId) =>
        this.beds.Any(bed =>
            (excludingBedId is null || bed.Id != excludingBedId.Value) &&
            bed.Label == label);

    private Result EnsureActive() =>
        this.Status switch
        {
            RoomState.Active => Result.Success(),
            RoomState.Retired => Result.Failure(PropertiesDomainErrors.RoomRetired),
            _ => Result.Failure(PropertiesDomainErrors.RoomStatusUnknown)
        };

    private static Result EnsureBedActive(Bed bed) =>
        bed.Status switch
        {
            BedState.Active => Result.Success(),
            BedState.Retired => Result.Failure(PropertiesDomainErrors.BedAlreadyRetired),
            _ => Result.Failure(PropertiesDomainErrors.BedStatusUnknown)
        };

    private Result EnsureExpectedVersion(long expectedVersion) =>
        expectedVersion == this.Version
            ? Result.Success()
            : Result.Failure(PropertiesDomainErrors.VersionConflict);

}

namespace Properties.Domain.Aggregates;

using Properties.Domain.Entities;
using Properties.Domain.Errors;
using Properties.Domain.Events;
using Properties.Domain.ValueObjects;
using Gma.Framework.Domain.Models;
using Gma.Framework.Naming;
using Gma.Framework.Results;

public sealed class Room : TenantAggregateRoot<Guid>
{
    public const int RoomNameMaxLength = 128;
    public const int PhysicalLabelMaxLength = 128;
    public const int BedLabelMaxLength = 128;

    private readonly List<Bed> beds = [];

    private Room() { }

    private Room(Guid id, string tenantId)
        : base(id, tenantId)
    {
    }

    public Guid PropertyId { get; private set; }
    public RoomName Name { get; private set; }
    public PhysicalLabel? BuildingLabel { get; private set; }
    public PhysicalLabel? FloorLabel { get; private set; }
    public RoomState Status { get; private set; } = RoomState.Active;
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

        Result<RoomValues> values = RoomValues.Create(tenantId, name, buildingLabel, floorLabel);
        if (values.IsFailure)
        {
            return Result.Failure<Room>(values.Error);
        }

        Room room = new(id, values.Value.TenantId)
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
            room.TenantId,
            room.Name.Value,
            room.BuildingLabel?.Value,
            room.FloorLabel?.Value,
            room.Status));

        return Result.Success(room);
    }

    public Result Update(
        string name,
        string? buildingLabel,
        string? floorLabel,
        Guid eventId,
        DateTimeOffset nowUtc)
    {
        Result statusResult = this.EnsureActive();
        if (statusResult.IsFailure)
        {
            return statusResult;
        }

        if (eventId == Guid.Empty)
        {
            return Result.Failure(PropertiesDomainErrors.DomainEventIdRequired);
        }

        Result<RoomValues> values = RoomValues.Create(this.TenantId, name, buildingLabel, floorLabel);
        if (values.IsFailure)
        {
            return Result.Failure(values.Error);
        }

        this.Name = values.Value.Name;
        this.BuildingLabel = values.Value.BuildingLabel;
        this.FloorLabel = values.Value.FloorLabel;
        this.UpdatedAtUtc = nowUtc;

        this.RaiseDomainEvent(new RoomUpdatedDomainEvent(
            eventId,
            nowUtc,
            this.PropertyId,
            this.Id,
            this.TenantId,
            this.Name.Value,
            this.BuildingLabel?.Value,
            this.FloorLabel?.Value,
            this.Status));

        return Result.Success();
    }

    public Result Retire(Guid eventId, DateTimeOffset nowUtc)
    {
        Result statusResult = this.EnsureActive();
        if (statusResult.IsFailure)
        {
            return statusResult;
        }

        if (eventId == Guid.Empty)
        {
            return Result.Failure(PropertiesDomainErrors.DomainEventIdRequired);
        }

        this.Status = RoomState.Retired;
        this.RetiredAtUtc = nowUtc;
        this.UpdatedAtUtc = nowUtc;

        this.RaiseDomainEvent(new RoomRetiredDomainEvent(
            eventId,
            nowUtc,
            this.PropertyId,
            this.Id,
            this.TenantId));

        return Result.Success();
    }

    public Result<Bed> AddBed(Guid bedId, string label, Guid eventId, DateTimeOffset nowUtc)
    {
        Result statusResult = this.EnsureActive();
        if (statusResult.IsFailure)
        {
            return Result.Failure<Bed>(statusResult.Error);
        }

        if (bedId == Guid.Empty)
        {
            return Result.Failure<Bed>(PropertiesDomainErrors.BedIdRequired);
        }

        if (eventId == Guid.Empty)
        {
            return Result.Failure<Bed>(PropertiesDomainErrors.DomainEventIdRequired);
        }

        Result<BedLabel> labelResult = BedLabel.Create(label);
        if (labelResult.IsFailure)
        {
            return Result.Failure<Bed>(labelResult.Error);
        }

        if (this.HasBedLabel(labelResult.Value, excludingBedId: null))
        {
            return Result.Failure<Bed>(PropertiesDomainErrors.BedAlreadyExists);
        }

        Bed bed = Bed.Create(bedId, this.TenantId, this.PropertyId, this.Id, labelResult.Value, nowUtc);
        this.beds.Add(bed);

        this.RaiseDomainEvent(new BedAddedDomainEvent(
            eventId,
            nowUtc,
            this.PropertyId,
            this.Id,
            bed.Id,
            this.TenantId,
            bed.Label.Value,
            bed.Status));

        return Result.Success(bed);
    }

    public Result<Bed> UpdateBed(Guid bedId, string label, Guid eventId, DateTimeOffset nowUtc)
    {
        Result statusResult = this.EnsureActive();
        if (statusResult.IsFailure)
        {
            return Result.Failure<Bed>(statusResult.Error);
        }

        if (eventId == Guid.Empty)
        {
            return Result.Failure<Bed>(PropertiesDomainErrors.DomainEventIdRequired);
        }

        Bed? bed = this.beds.FirstOrDefault(candidate => candidate.Id == bedId);
        if (bed is null)
        {
            return Result.Failure<Bed>(PropertiesDomainErrors.BedNotFound);
        }

        Result bedStatusResult = EnsureBedActive(bed);
        if (bedStatusResult.IsFailure)
        {
            return Result.Failure<Bed>(bedStatusResult.Error);
        }

        Result<BedLabel> labelResult = BedLabel.Create(label);
        if (labelResult.IsFailure)
        {
            return Result.Failure<Bed>(labelResult.Error);
        }

        if (this.HasBedLabel(labelResult.Value, excludingBedId: bed.Id))
        {
            return Result.Failure<Bed>(PropertiesDomainErrors.BedAlreadyExists);
        }

        bed.Update(labelResult.Value, nowUtc);
        this.UpdatedAtUtc = nowUtc;

        this.RaiseDomainEvent(new BedUpdatedDomainEvent(
            eventId,
            nowUtc,
            this.PropertyId,
            this.Id,
            bed.Id,
            this.TenantId,
            bed.Label.Value,
            bed.Status));

        return Result.Success(bed);
    }

    public Result RetireBed(Guid bedId, Guid eventId, DateTimeOffset nowUtc)
    {
        Result statusResult = this.EnsureActive();
        if (statusResult.IsFailure)
        {
            return statusResult;
        }

        if (eventId == Guid.Empty)
        {
            return Result.Failure(PropertiesDomainErrors.DomainEventIdRequired);
        }

        Bed? bed = this.beds.FirstOrDefault(candidate => candidate.Id == bedId);
        if (bed is null)
        {
            return Result.Failure(PropertiesDomainErrors.BedNotFound);
        }

        Result bedStatusResult = EnsureBedActive(bed);
        if (bedStatusResult.IsFailure)
        {
            return bedStatusResult;
        }

        bed.Retire(nowUtc);
        this.UpdatedAtUtc = nowUtc;

        this.RaiseDomainEvent(new BedRetiredDomainEvent(
            eventId,
            nowUtc,
            this.PropertyId,
            this.Id,
            bed.Id,
            this.TenantId));

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

    private sealed record RoomValues(
        string TenantId,
        RoomName Name,
        PhysicalLabel? BuildingLabel,
        PhysicalLabel? FloorLabel)
    {
        public static Result<RoomValues> Create(
            string tenantId,
            string? name,
            string? buildingLabel,
            string? floorLabel)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
            {
                return Result.Failure<RoomValues>(PropertiesDomainErrors.TenantRequired);
            }

            if (!TenantIds.TryNormalize(tenantId, out string? normalizedTenantId))
            {
                return Result.Failure<RoomValues>(PropertiesDomainErrors.TenantInvalid);
            }

            Result<RoomName> nameResult = RoomName.Create(name);
            if (nameResult.IsFailure)
            {
                return Result.Failure<RoomValues>(nameResult.Error);
            }

            PhysicalLabel? normalizedBuildingLabel = null;
            if (!string.IsNullOrWhiteSpace(buildingLabel))
            {
                Result<PhysicalLabel> buildingLabelResult = PhysicalLabel.Create(buildingLabel);
                if (buildingLabelResult.IsFailure)
                {
                    return Result.Failure<RoomValues>(buildingLabelResult.Error);
                }

                normalizedBuildingLabel = buildingLabelResult.Value;
            }

            PhysicalLabel? normalizedFloorLabel = null;
            if (!string.IsNullOrWhiteSpace(floorLabel))
            {
                Result<PhysicalLabel> floorLabelResult = PhysicalLabel.Create(floorLabel);
                if (floorLabelResult.IsFailure)
                {
                    return Result.Failure<RoomValues>(floorLabelResult.Error);
                }

                normalizedFloorLabel = floorLabelResult.Value;
            }

            return Result.Success(new RoomValues(
                normalizedTenantId,
                nameResult.Value,
                normalizedBuildingLabel,
                normalizedFloorLabel));
        }
    }
}

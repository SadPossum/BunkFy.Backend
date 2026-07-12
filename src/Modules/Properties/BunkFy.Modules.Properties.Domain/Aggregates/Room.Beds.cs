namespace BunkFy.Modules.Properties.Domain.Aggregates;

using BunkFy.Modules.Properties.Domain.Entities;
using BunkFy.Modules.Properties.Domain.Errors;
using BunkFy.Modules.Properties.Domain.Events;
using BunkFy.Modules.Properties.Domain.ValueObjects;
using Gma.Framework.Results;

public sealed partial class Room
{
    public Result<Bed> AddBed(
        Guid bedId,
        string label,
        long expectedVersion,
        Guid eventId,
        DateTimeOffset nowUtc)
    {
        Result statusResult = this.EnsureActive();
        if (statusResult.IsFailure)
        {
            return Result.Failure<Bed>(statusResult.Error);
        }

        Result versionResult = this.EnsureExpectedVersion(expectedVersion);
        if (versionResult.IsFailure)
        {
            return Result.Failure<Bed>(versionResult.Error);
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

        Bed bed = Bed.Create(bedId, this.ScopeId, this.PropertyId, this.Id, labelResult.Value, nowUtc);
        this.beds.Add(bed);
        this.UpdatedAtUtc = nowUtc;
        this.Version++;

        this.RaiseDomainEvent(new BedAddedDomainEvent(
            eventId,
            nowUtc,
            this.PropertyId,
            this.Id,
            bed.Id,
            this.ScopeId,
            bed.Label.Value,
            bed.Status,
            this.Version,
            bed.Version));

        return Result.Success(bed);
    }

    public Result<Bed> UpdateBed(
        Guid bedId,
        string label,
        long expectedVersion,
        Guid eventId,
        DateTimeOffset nowUtc)
    {
        Result statusResult = this.EnsureActive();
        if (statusResult.IsFailure)
        {
            return Result.Failure<Bed>(statusResult.Error);
        }

        Result versionResult = this.EnsureExpectedVersion(expectedVersion);
        if (versionResult.IsFailure)
        {
            return Result.Failure<Bed>(versionResult.Error);
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
        this.Version++;

        this.RaiseDomainEvent(new BedUpdatedDomainEvent(
            eventId,
            nowUtc,
            this.PropertyId,
            this.Id,
            bed.Id,
            this.ScopeId,
            bed.Label.Value,
            bed.Status,
            this.Version,
            bed.Version));

        return Result.Success(bed);
    }

    public Result RetireBed(Guid bedId, long expectedVersion, Guid eventId, DateTimeOffset nowUtc)
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
        this.Version++;

        this.RaiseDomainEvent(new BedRetiredDomainEvent(
            eventId,
            nowUtc,
            this.PropertyId,
            this.Id,
            bed.Id,
            this.ScopeId,
            this.Version,
            bed.Version));

        return Result.Success();
    }

}

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
        Result<IReadOnlyCollection<Bed>> result = this.AddBeds(
            [new BedAdditionDefinition(bedId, label, eventId)],
            expectedVersion,
            nowUtc);
        return result.IsSuccess
            ? Result.Success(result.Value.Single())
            : Result.Failure<Bed>(result.Error);
    }

    public Result<IReadOnlyCollection<Bed>> AddBeds(
        IReadOnlyCollection<BedAdditionDefinition> additions,
        long expectedVersion,
        DateTimeOffset nowUtc)
    {
        Result statusResult = this.EnsureActive();
        if (statusResult.IsFailure)
        {
            return Result.Failure<IReadOnlyCollection<Bed>>(statusResult.Error);
        }

        Result versionResult = this.EnsureExpectedVersion(expectedVersion);
        if (versionResult.IsFailure)
        {
            return Result.Failure<IReadOnlyCollection<Bed>>(versionResult.Error);
        }

        if (additions is null || additions.Count == 0)
        {
            return Result.Failure<IReadOnlyCollection<Bed>>(PropertiesDomainErrors.BedBatchRequired);
        }

        BedAdditionDefinition[] requested = [.. additions];
        if (requested.Any(addition => addition.BedId == Guid.Empty || addition.EventId == Guid.Empty) ||
            requested.Select(addition => addition.BedId).Distinct().Count() != requested.Length ||
            requested.Select(addition => addition.EventId).Distinct().Count() != requested.Length ||
            requested.Any(addition => this.beds.Any(bed => bed.Id == addition.BedId)))
        {
            return Result.Failure<IReadOnlyCollection<Bed>>(PropertiesDomainErrors.BedBatchInvalid);
        }

        List<BedLabel> labels = new(requested.Length);
        foreach (BedAdditionDefinition addition in requested)
        {
            Result<BedLabel> labelResult = BedLabel.Create(addition.Label);
            if (labelResult.IsFailure)
            {
                return Result.Failure<IReadOnlyCollection<Bed>>(labelResult.Error);
            }

            labels.Add(labelResult.Value);
        }

        if (labels.Distinct().Count() != labels.Count ||
            labels.Any(label => this.HasBedLabel(label, excludingBedId: null)))
        {
            return Result.Failure<IReadOnlyCollection<Bed>>(PropertiesDomainErrors.BedAlreadyExists);
        }

        Bed[] created = requested
            .Select((addition, index) => Bed.Create(
                addition.BedId,
                this.ScopeId,
                this.PropertyId,
                this.Id,
                labels[index],
                nowUtc))
            .ToArray();

        for (int index = 0; index < created.Length; index++)
        {
            Bed bed = created[index];
            this.beds.Add(bed);
            this.Version++;

            this.RaiseDomainEvent(new BedAddedDomainEvent(
                requested[index].EventId,
                nowUtc,
                this.PropertyId,
                this.Id,
                bed.Id,
                this.ScopeId,
                bed.Label.Value,
                bed.Status,
                this.Version,
                bed.Version));
        }

        this.UpdatedAtUtc = nowUtc;

        return Result.Success<IReadOnlyCollection<Bed>>(created);
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

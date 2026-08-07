namespace BunkFy.Modules.Guests.Domain.Aggregates;

using BunkFy.Modules.Guests.Domain.ValueObjects;
using Gma.Framework.Results;

public sealed class GuestProfileCreationSnapshot
{
    private GuestProfileCreationSnapshot(
        Guid originPropertyId,
        GuestProfileChange values)
    {
        this.OriginPropertyId = originPropertyId;
        this.Values = values;
    }

    public Guid OriginPropertyId { get; }

    internal GuestProfileChange Values { get; }

    public static Result<GuestProfileCreationSnapshot> Capture(
        Guid originPropertyId,
        string? displayName,
        string? legalName,
        string? email,
        string? phone,
        DateOnly? dateOfBirth,
        string? nationalityCountryCode,
        string? preferredLanguageTag,
        string? notes,
        string? actorId,
        DateTimeOffset nowUtc)
    {
        Result<GuestProfileChange> values = GuestProfileChange.Create(
            displayName,
            legalName,
            email,
            phone,
            dateOfBirth,
            nationalityCountryCode,
            preferredLanguageTag,
            notes,
            actorId,
            nowUtc);
        return values.IsSuccess
            ? Result.Success(new GuestProfileCreationSnapshot(originPropertyId, values.Value))
            : Result.Failure<GuestProfileCreationSnapshot>(values.Error);
    }
}

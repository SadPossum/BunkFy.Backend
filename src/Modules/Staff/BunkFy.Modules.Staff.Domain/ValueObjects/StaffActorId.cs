namespace BunkFy.Modules.Staff.Domain.ValueObjects;

using Gma.Framework.Results;
using BunkFy.Modules.Staff.Domain.Errors;

public sealed record StaffActorId
{
    public const int MaxLength = 200;

    private StaffActorId(string value) => this.Value = value;

    public string Value { get; }

    public static Result<StaffActorId> Create(string? value)
    {
        string normalized = value?.Trim() ?? string.Empty;
        return normalized.Length is > 0 and <= MaxLength
            ? Result.Success(new StaffActorId(normalized))
            : Result.Failure<StaffActorId>(StaffDomainErrors.ActorInvalid);
    }
}

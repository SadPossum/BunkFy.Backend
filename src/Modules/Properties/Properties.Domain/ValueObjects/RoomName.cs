namespace Properties.Domain.ValueObjects;

using Properties.Domain.Aggregates;
using Properties.Domain.Errors;
using Gma.Framework.Results;

public readonly record struct RoomName
{
    private readonly string? value;

    private RoomName(string value) => this.value = value;

    public string Value => this.value ?? string.Empty;

    public static Result<RoomName> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result.Failure<RoomName>(PropertiesDomainErrors.RoomNameRequired);
        }

        string normalized = value.Trim();
        return normalized.Length <= Room.RoomNameMaxLength
            ? Result.Success(new RoomName(normalized))
            : Result.Failure<RoomName>(PropertiesDomainErrors.RoomNameTooLong);
    }

    public override string ToString() => this.Value;
}

namespace BunkFy.Modules.Properties.Domain.ValueObjects;

using BunkFy.Modules.Properties.Domain.Aggregates;
using BunkFy.Modules.Properties.Domain.Errors;
using Gma.Framework.Results;

public readonly record struct PropertyCode
{
    private readonly string? value;

    private PropertyCode(string value) => this.value = value;

    public string Value => this.value ?? string.Empty;

    public static Result<PropertyCode> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result.Failure<PropertyCode>(PropertiesDomainErrors.PropertyCodeRequired);
        }

        string normalized = value.Trim().ToLowerInvariant();
        if (normalized.Length > Property.PropertyCodeMaxLength)
        {
            return Result.Failure<PropertyCode>(PropertiesDomainErrors.PropertyCodeTooLong);
        }

        return IsValid(normalized)
            ? Result.Success(new PropertyCode(normalized))
            : Result.Failure<PropertyCode>(PropertiesDomainErrors.PropertyCodeInvalid);
    }

    public static string Normalize(string? value) => Create(value).Value.Value;

    public override string ToString() => this.Value;

    private static bool IsValid(string value) =>
        value.Length > 0 &&
        value[0] != '-' &&
        value[^1] != '-' &&
        value.All(character =>
            character is (>= 'a' and <= 'z') or
            (>= '0' and <= '9') or
            '-');
}

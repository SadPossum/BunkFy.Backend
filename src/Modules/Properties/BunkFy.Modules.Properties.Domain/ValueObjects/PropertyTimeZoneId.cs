namespace BunkFy.Modules.Properties.Domain.ValueObjects;

using BunkFy.Modules.Properties.Domain.Aggregates;
using BunkFy.Modules.Properties.Domain.Errors;
using Gma.Framework.Results;

public readonly record struct PropertyTimeZoneId
{
    private readonly string? value;

    private PropertyTimeZoneId(string value) => this.value = value;

    public string Value => this.value ?? string.Empty;

    public static Result<PropertyTimeZoneId> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result.Failure<PropertyTimeZoneId>(PropertiesDomainErrors.TimeZoneRequired);
        }

        string normalized = value.Trim();
        if (normalized.Length > Property.TimeZoneIdMaxLength)
        {
            return Result.Failure<PropertyTimeZoneId>(PropertiesDomainErrors.TimeZoneTooLong);
        }

        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(normalized);
            return Result.Success(new PropertyTimeZoneId(normalized));
        }
        catch (TimeZoneNotFoundException)
        {
            return Result.Failure<PropertyTimeZoneId>(PropertiesDomainErrors.TimeZoneInvalid);
        }
        catch (InvalidTimeZoneException)
        {
            return Result.Failure<PropertyTimeZoneId>(PropertiesDomainErrors.TimeZoneInvalid);
        }
    }

    public override string ToString() => this.Value;
}

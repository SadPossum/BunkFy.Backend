namespace BunkFy.Modules.Properties.Domain.ValueObjects;

using BunkFy.Modules.Properties.Domain.Aggregates;
using BunkFy.Modules.Properties.Domain.Errors;
using Gma.Framework.Results;

public readonly record struct PropertyName
{
    private readonly string? value;

    private PropertyName(string value) => this.value = value;

    public string Value => this.value ?? string.Empty;

    public static Result<PropertyName> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result.Failure<PropertyName>(PropertiesDomainErrors.PropertyNameRequired);
        }

        string normalized = value.Trim();
        return normalized.Length <= Property.PropertyNameMaxLength
            ? Result.Success(new PropertyName(normalized))
            : Result.Failure<PropertyName>(PropertiesDomainErrors.PropertyNameTooLong);
    }

    public override string ToString() => this.Value;
}

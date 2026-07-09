namespace Properties.Domain.ValueObjects;

using Properties.Domain.Aggregates;
using Properties.Domain.Errors;
using Gma.Framework.Results;

public readonly record struct PhysicalLabel
{
    private readonly string? value;

    private PhysicalLabel(string value) => this.value = value;

    public string Value => this.value ?? string.Empty;

    public static Result<PhysicalLabel> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result.Failure<PhysicalLabel>(PropertiesDomainErrors.PhysicalLabelTooLong);
        }

        string normalized = value.Trim();
        return normalized.Length <= Room.PhysicalLabelMaxLength
            ? Result.Success(new PhysicalLabel(normalized))
            : Result.Failure<PhysicalLabel>(PropertiesDomainErrors.PhysicalLabelTooLong);
    }

    public override string ToString() => this.Value;
}

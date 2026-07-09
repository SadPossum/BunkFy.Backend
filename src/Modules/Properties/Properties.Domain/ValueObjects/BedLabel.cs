namespace Properties.Domain.ValueObjects;

using Properties.Domain.Aggregates;
using Properties.Domain.Errors;
using Gma.Framework.Results;

public readonly record struct BedLabel
{
    private readonly string? value;

    private BedLabel(string value) => this.value = value;

    public string Value => this.value ?? string.Empty;

    public static Result<BedLabel> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result.Failure<BedLabel>(PropertiesDomainErrors.BedLabelRequired);
        }

        string normalized = value.Trim();
        return normalized.Length <= Room.BedLabelMaxLength
            ? Result.Success(new BedLabel(normalized))
            : Result.Failure<BedLabel>(PropertiesDomainErrors.BedLabelTooLong);
    }

    public override string ToString() => this.Value;
}

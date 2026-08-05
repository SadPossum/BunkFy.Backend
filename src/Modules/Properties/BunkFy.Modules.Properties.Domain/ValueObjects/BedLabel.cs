namespace BunkFy.Modules.Properties.Domain.ValueObjects;

using BunkFy.Modules.Properties.Domain.Aggregates;
using BunkFy.Modules.Properties.Domain.Errors;
using Gma.Framework.Results;

public readonly record struct BedLabel : IComparable<BedLabel>
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

    public int CompareTo(BedLabel other) =>
        StringComparer.Ordinal.Compare(this.Value, other.Value);

    public static bool operator <(BedLabel left, BedLabel right) => left.CompareTo(right) < 0;
    public static bool operator <=(BedLabel left, BedLabel right) => left.CompareTo(right) <= 0;
    public static bool operator >(BedLabel left, BedLabel right) => left.CompareTo(right) > 0;
    public static bool operator >=(BedLabel left, BedLabel right) => left.CompareTo(right) >= 0;

    public override string ToString() => this.Value;
}

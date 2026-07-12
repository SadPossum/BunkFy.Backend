namespace BunkFy.Modules.Staff.Domain.ValueObjects;

using Gma.Framework.Results;
using BunkFy.Modules.Staff.Domain.Errors;

public sealed record StaffChangeReason
{
    public const int MaxLength = 1000;

    private StaffChangeReason(string value) => this.Value = value;

    public string Value { get; }

    public static Result<StaffChangeReason> Create(string? value)
    {
        string normalized = value?.Trim() ?? string.Empty;
        return normalized.Length is > 0 and <= MaxLength
            ? Result.Success(new StaffChangeReason(normalized))
            : Result.Failure<StaffChangeReason>(StaffDomainErrors.ReasonInvalid);
    }
}

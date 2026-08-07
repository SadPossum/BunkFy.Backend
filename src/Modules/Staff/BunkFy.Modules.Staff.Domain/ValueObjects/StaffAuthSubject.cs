namespace BunkFy.Modules.Staff.Domain.ValueObjects;

using BunkFy.Modules.Staff.Domain.Errors;
using Gma.Framework.Results;

public sealed record StaffAuthSubject
{
    public const int MaxLength = 256;

    private StaffAuthSubject(string? value) => this.Value = value;

    public string? Value { get; }

    public static Result<StaffAuthSubject> Create(string? value)
    {
        string normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length > MaxLength)
        {
            return Result.Failure<StaffAuthSubject>(
                StaffDomainErrors.AuthSubjectInvalid);
        }

        return Result.Success(new StaffAuthSubject(
            normalized.Length == 0 ? null : normalized));
    }
}

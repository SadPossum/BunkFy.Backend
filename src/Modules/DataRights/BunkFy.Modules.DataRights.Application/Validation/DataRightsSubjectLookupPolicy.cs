namespace BunkFy.Modules.DataRights.Application.Validation;

using System.Net.Mail;
using BunkFy.Modules.DataRights.Contracts;
using Gma.Framework.Results;

internal static class DataRightsSubjectLookupPolicy
{
    public static IEnumerable<string> Validate(DataRightsSubjectLookup? lookup)
    {
        if (lookup is null)
        {
            yield return "Lookup is required.";
            yield break;
        }

        int strongCoordinates =
            (lookup.RecordId.HasValue ? 1 : 0) +
            (!string.IsNullOrWhiteSpace(lookup.Email) ? 1 : 0) +
            (!string.IsNullOrWhiteSpace(lookup.Phone) ? 1 : 0);
        if (strongCoordinates != 1 || lookup.RecordId == Guid.Empty)
        {
            yield return "Exactly one non-empty guest id, email, or phone is required.";
        }

        string? email = NormalizeOptional(lookup.Email)?.ToLowerInvariant();
        if (email is not null &&
            (email.Length > DataRightsSubjectDiscoveryLimits.ContactHintMaxLength ||
             !MailAddress.TryCreate(email, out _)))
        {
            yield return "Email must be a valid address within the supported limit.";
        }

        string? phone = NormalizeOptional(lookup.Phone);
        if (phone?.Length > 64)
        {
            yield return "Phone must be within the supported limit.";
        }

        string? name = NormalizeOptional(lookup.Name);
        if (name?.Length > DataRightsSubjectDiscoveryLimits.DisplayNameMaxLength)
        {
            yield return "Name must be within the supported limit.";
        }
    }

    public static Result<DataRightsSubjectLookup> Normalize(DataRightsSubjectLookup? lookup)
    {
        if (Validate(lookup).Any())
        {
            return Result.Failure<DataRightsSubjectLookup>(
                DataRightsApplicationErrors.DiscoveryCriteriaInvalid);
        }

        return Result.Success(new DataRightsSubjectLookup(
            lookup!.RecordId,
            NormalizeOptional(lookup.Email)?.ToLowerInvariant(),
            NormalizeOptional(lookup.Phone),
            NormalizeOptional(lookup.Name),
            lookup.DateOfBirth));
    }

    private static string? NormalizeOptional(string? value)
    {
        string normalized = value?.Trim() ?? string.Empty;
        return normalized.Length == 0 ? null : normalized;
    }
}

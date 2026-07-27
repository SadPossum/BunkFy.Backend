namespace BunkFy.Modules.DataRights.Application.Handlers;

using BunkFy.Modules.DataRights.Contracts;
using Gma.Framework.Results;

internal static class DataRightsExportContributorSet
{
    public static Result<IReadOnlyDictionary<string, IDataRightsSubjectExportContributor>>
        Resolve(
            IEnumerable<IDataRightsSubjectExportContributor> contributors,
            DataRightsCaseType caseType,
            IEnumerable<DataRightsSubjectCoordinate> subjects)
    {
        if (contributors is null ||
            subjects is null ||
            caseType is not DataRightsCaseType.GuestRights and
                not DataRightsCaseType.StaffRights)
        {
            return Failure();
        }

        IDataRightsSubjectExportContributor[] available = contributors
            .Where(contributor => contributor is not null &&
                contributor.SupportedCaseTypes?.Contains(caseType) == true)
            .ToArray();
        if (available.Any(contributor =>
                string.IsNullOrWhiteSpace(contributor.OwnerKey) ||
                contributor.OwnerKey.Trim().Length >
                    DataRightsExportLimits.OwnerKeyMaxLength ||
                contributor.Descriptor is null) ||
            available.GroupBy(
                    contributor => contributor.OwnerKey.Trim().ToLowerInvariant(),
                    StringComparer.Ordinal)
                .Any(group => group.Count() != 1))
        {
            return Failure();
        }

        Dictionary<string, IDataRightsSubjectExportContributor> byOwner =
            available.ToDictionary(
                contributor => contributor.OwnerKey.Trim().ToLowerInvariant(),
                StringComparer.Ordinal);
        string[] requiredOwners = subjects
            .Select(subject => subject.OwnerKey?.Trim().ToLowerInvariant() ?? string.Empty)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (requiredOwners.Length == 0 ||
            requiredOwners.Any(owner => !byOwner.ContainsKey(owner)))
        {
            return Failure();
        }

        return Result.Success<IReadOnlyDictionary<
            string,
            IDataRightsSubjectExportContributor>>(byOwner);
    }

    private static Result<IReadOnlyDictionary<
        string,
        IDataRightsSubjectExportContributor>> Failure() =>
        Result.Failure<IReadOnlyDictionary<
            string,
            IDataRightsSubjectExportContributor>>(
                DataRightsApplicationErrors.ExportOwnerUnavailable);
}

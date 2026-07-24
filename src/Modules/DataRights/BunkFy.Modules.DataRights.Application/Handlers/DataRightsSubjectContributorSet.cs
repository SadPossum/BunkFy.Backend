namespace BunkFy.Modules.DataRights.Application.Handlers;

using BunkFy.Modules.DataRights.Contracts;
using Gma.Framework.Results;

internal static class DataRightsSubjectContributorSet
{
    public static Result<IReadOnlyCollection<IDataRightsSubjectDiscoveryContributor>> Order(
        IEnumerable<IDataRightsSubjectDiscoveryContributor> contributors)
    {
        if (contributors is null)
        {
            return Result.Failure<IReadOnlyCollection<IDataRightsSubjectDiscoveryContributor>>(
                DataRightsApplicationErrors.SubjectOwnerUnavailable);
        }

        IDataRightsSubjectDiscoveryContributor[] supplied = contributors.ToArray();
        if (supplied.Length == 0 ||
            supplied.Any(contributor =>
                contributor is null ||
                string.IsNullOrWhiteSpace(contributor.OwnerKey) ||
                contributor.OwnerKey.Trim().Length >
                    DataRightsSubjectDiscoveryLimits.OwnerKeyMaxLength) ||
            supplied.GroupBy(
                    contributor => contributor.OwnerKey.Trim().ToLowerInvariant(),
                    StringComparer.Ordinal)
                .Any(group => group.Count() != 1))
        {
            return Result.Failure<IReadOnlyCollection<IDataRightsSubjectDiscoveryContributor>>(
                DataRightsApplicationErrors.SubjectOwnerUnavailable);
        }

        IDataRightsSubjectDiscoveryContributor[] ordered = supplied
            .OrderBy(
                contributor => contributor.OwnerKey.Trim().ToLowerInvariant(),
                StringComparer.Ordinal)
            .ToArray();
        return Result.Success<IReadOnlyCollection<IDataRightsSubjectDiscoveryContributor>>(ordered);
    }

    public static Result<IDataRightsSubjectDiscoveryContributor> Find(
        IEnumerable<IDataRightsSubjectDiscoveryContributor> contributors,
        string ownerKey)
    {
        string normalizedOwner = ownerKey?.Trim().ToLowerInvariant() ?? string.Empty;
        if (normalizedOwner.Length == 0)
        {
            return Result.Failure<IDataRightsSubjectDiscoveryContributor>(
                DataRightsApplicationErrors.SubjectOwnerUnavailable);
        }

        Result<IReadOnlyCollection<IDataRightsSubjectDiscoveryContributor>> ordered =
            Order(contributors);
        if (ordered.IsFailure)
        {
            return Result.Failure<IDataRightsSubjectDiscoveryContributor>(ordered.Error);
        }

        IDataRightsSubjectDiscoveryContributor[] matches = ordered.Value
            .Where(contributor => string.Equals(
                contributor.OwnerKey.Trim(),
                normalizedOwner,
                StringComparison.OrdinalIgnoreCase))
            .ToArray();
        return matches.Length == 1
            ? Result.Success(matches[0])
            : Result.Failure<IDataRightsSubjectDiscoveryContributor>(
                DataRightsApplicationErrors.SubjectOwnerUnavailable);
    }
}

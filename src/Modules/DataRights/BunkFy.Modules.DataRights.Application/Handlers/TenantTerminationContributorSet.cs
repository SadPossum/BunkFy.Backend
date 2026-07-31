namespace BunkFy.Modules.DataRights.Application.Handlers;

using BunkFy.Modules.DataRights.Contracts;
using Gma.Framework.Results;

internal static class TenantTerminationContributorSet
{
    public static Result<IReadOnlyList<ITenantTerminationContributor>> OrderForPhase(
        IEnumerable<ITenantTerminationContributor> contributors,
        TenantTerminationContributionPhase phase)
    {
        if (!Enum.IsDefined(phase) ||
            phase == TenantTerminationContributionPhase.Unknown ||
            !TryValidateAndIndex(
                contributors,
                out Dictionary<string, ITenantTerminationContributor> index))
        {
            return Invalid<IReadOnlyList<ITenantTerminationContributor>>();
        }

        List<ITenantTerminationContributor> ordered = [];
        Dictionary<string, int> incoming = index.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.Descriptor.DependsOnOwnerKeys.Count,
            StringComparer.Ordinal);
        SortedSet<string> ready = new(
            incoming.Where(pair => pair.Value == 0).Select(pair => pair.Key),
            StringComparer.Ordinal);

        while (ready.Count > 0)
        {
            string ownerKey = ready.Min!;
            ready.Remove(ownerKey);
            ITenantTerminationContributor contributor = index[ownerKey];
            ordered.Add(contributor);

            foreach (KeyValuePair<string, ITenantTerminationContributor> candidate in index)
            {
                if (!candidate.Value.Descriptor.DependsOnOwnerKeys.Contains(
                        ownerKey,
                        StringComparer.Ordinal))
                {
                    continue;
                }

                incoming[candidate.Key]--;
                if (incoming[candidate.Key] == 0)
                {
                    ready.Add(candidate.Key);
                }
            }
        }

        if (ordered.Count != index.Count)
        {
            return Invalid<IReadOnlyList<ITenantTerminationContributor>>();
        }

        ITenantTerminationContributor[] phaseOwners = ordered
            .Where(contributor =>
                contributor.Descriptor.SupportedPhases.Contains(phase))
            .ToArray();
        return phaseOwners.Length > 0
            ? Result.Success<IReadOnlyList<ITenantTerminationContributor>>(
                phaseOwners)
            : Invalid<IReadOnlyList<ITenantTerminationContributor>>();
    }

    public static Result ValidateProductionCatalog(
        IEnumerable<ITenantTerminationContributor> contributors,
        IReadOnlyCollection<string> requiredOwnerKeys)
    {
        if (!TryValidateAndIndex(
                contributors,
                out Dictionary<string, ITenantTerminationContributor> index) ||
            HasDependencyCycle(index) ||
            requiredOwnerKeys is null ||
            requiredOwnerKeys.Count is <= 0 or >
                TenantTerminationContract.MaximumContributors)
        {
            return Invalid();
        }

        string[] required = requiredOwnerKeys
            .Select(ownerKey => ownerKey?.Trim() ?? string.Empty)
            .ToArray();
        if (required.Any(ownerKey => !IsStableKey(ownerKey)) ||
            required.Distinct(StringComparer.Ordinal).Count() !=
                required.Length)
        {
            return Invalid();
        }

        string[] declaredMandatory = index.Values
            .Where(contributor =>
                contributor.Descriptor.MandatoryForProduction)
            .Select(contributor => contributor.Descriptor.OwnerKey)
            .OrderBy(ownerKey => ownerKey, StringComparer.Ordinal)
            .ToArray();
        string[] expected = required
            .OrderBy(ownerKey => ownerKey, StringComparer.Ordinal)
            .ToArray();
        return declaredMandatory.SequenceEqual(expected, StringComparer.Ordinal)
            ? Result.Success()
            : Invalid();
    }

    private static bool TryValidateAndIndex(
        IEnumerable<ITenantTerminationContributor>? contributors,
        out Dictionary<string, ITenantTerminationContributor> index)
    {
        index = new(StringComparer.Ordinal);
        if (contributors is null)
        {
            return false;
        }

        ITenantTerminationContributor[] supplied = contributors.ToArray();
        if (supplied.Length is <= 0 or >
            TenantTerminationContract.MaximumContributors)
        {
            return false;
        }

        foreach (ITenantTerminationContributor? contributor in supplied)
        {
            TenantTerminationContributorDescriptor? descriptor =
                contributor?.Descriptor;
            if (descriptor is null ||
                !IsStableKey(descriptor.OwnerKey) ||
                descriptor.ContractVersion !=
                    TenantTerminationContract.CurrentVersion ||
                descriptor.SupportedPhases is null ||
                descriptor.SupportedPhases.Count == 0 ||
                descriptor.SupportedPhases.Any(phase =>
                    !Enum.IsDefined(phase) ||
                    phase == TenantTerminationContributionPhase.Unknown) ||
                descriptor.SupportedPhases.Distinct().Count() !=
                    descriptor.SupportedPhases.Count ||
                descriptor.DependsOnOwnerKeys is null ||
                descriptor.DependsOnOwnerKeys.Count >
                    TenantTerminationContract.MaximumDependencies ||
                descriptor.DependsOnOwnerKeys.Any(ownerKey =>
                    !IsStableKey(ownerKey) ||
                    string.Equals(
                        ownerKey,
                        descriptor.OwnerKey,
                        StringComparison.Ordinal)) ||
                descriptor.DependsOnOwnerKeys.Distinct(StringComparer.Ordinal)
                    .Count() != descriptor.DependsOnOwnerKeys.Count ||
                descriptor.CatalogVersion <= 0 ||
                !IsSha256(descriptor.CatalogSha256) ||
                !index.TryAdd(descriptor.OwnerKey, contributor!))
            {
                index.Clear();
                return false;
            }
        }

        Dictionary<string, ITenantTerminationContributor> validatedIndex = index;
        bool dependencyMissing = validatedIndex.Values.Any(contributor =>
            contributor.Descriptor.DependsOnOwnerKeys.Any(
                dependency => !validatedIndex.ContainsKey(dependency)));
        if (dependencyMissing)
        {
            index.Clear();
            return false;
        }

        return true;
    }

    private static bool HasDependencyCycle(
        Dictionary<string, ITenantTerminationContributor> index)
    {
        Dictionary<string, int> incoming = index.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.Descriptor.DependsOnOwnerKeys.Count,
            StringComparer.Ordinal);
        Queue<string> ready = new(
            incoming.Where(pair => pair.Value == 0).Select(pair => pair.Key));
        int visited = 0;
        while (ready.Count > 0)
        {
            string ownerKey = ready.Dequeue();
            visited++;
            foreach (KeyValuePair<string, ITenantTerminationContributor> candidate in index)
            {
                if (!candidate.Value.Descriptor.DependsOnOwnerKeys.Contains(
                        ownerKey,
                        StringComparer.Ordinal))
                {
                    continue;
                }

                incoming[candidate.Key]--;
                if (incoming[candidate.Key] == 0)
                {
                    ready.Enqueue(candidate.Key);
                }
            }
        }

        return visited != index.Count;
    }

    private static bool IsStableKey(string? value)
    {
        string normalized = value?.Trim() ?? string.Empty;
        return normalized.Length > 0 &&
            normalized.Length <= TenantTerminationContract.OwnerKeyMaxLength &&
            normalized[0] is >= 'a' and <= 'z' &&
            normalized.All(character =>
                character is (>= 'a' and <= 'z') or
                    (>= '0' and <= '9') or '.' or '-' or '_');
    }

    private static bool IsSha256(string? value) =>
        value is { Length: TenantTerminationContract.Sha256Length } &&
        value.All(character =>
            character is (>= '0' and <= '9') or (>= 'a' and <= 'f'));

    private static Result Invalid() =>
        Result.Failure(
            DataRightsApplicationErrors
                .TenantTerminationContributorCatalogInvalid);

    private static Result<T> Invalid<T>() =>
        Result.Failure<T>(
            DataRightsApplicationErrors
                .TenantTerminationContributorCatalogInvalid);
}

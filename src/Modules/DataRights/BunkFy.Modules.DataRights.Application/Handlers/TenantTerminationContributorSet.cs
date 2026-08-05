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
                out Dictionary<string, ITenantTerminationContributor> index) ||
            HasDependencyCycle(index))
        {
            return Invalid<IReadOnlyList<ITenantTerminationContributor>>();
        }

        Dictionary<string, TenantTerminationContributorPhasePlan> phasePlans =
            index.Values
                .Select(contributor => new
                {
                    Contributor = contributor,
                    Plan = PhasePlanFor(contributor.Descriptor, phase)
                })
                .Where(item => item.Plan is not null)
                .ToDictionary(
                    item => item.Contributor.Descriptor.OwnerKey,
                    item => item.Plan!,
                    StringComparer.Ordinal);
        if (phasePlans.Count == 0)
        {
            return Invalid<IReadOnlyList<ITenantTerminationContributor>>();
        }

        List<ITenantTerminationContributor> ordered = [];
        Dictionary<string, int> incoming = phasePlans.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.DependsOnOwnerKeys.Count,
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

            foreach (KeyValuePair<string, TenantTerminationContributorPhasePlan> candidate in phasePlans)
            {
                if (!candidate.Value.DependsOnOwnerKeys.Contains(
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

        if (ordered.Count != phasePlans.Count)
        {
            return Invalid<IReadOnlyList<ITenantTerminationContributor>>();
        }

        return Result.Success<IReadOnlyList<ITenantTerminationContributor>>(
            ordered);
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
                !HasValidPhasePlans(descriptor) ||
                descriptor.CatalogVersion <= 0 ||
                !IsSha256(descriptor.CatalogSha256) ||
                !index.TryAdd(descriptor.OwnerKey, contributor!))
            {
                index.Clear();
                return false;
            }
        }

        Dictionary<string, ITenantTerminationContributor> validatedIndex = index;
        bool dependencyInvalid = validatedIndex.Values.Any(contributor =>
            contributor.Descriptor.PhasePlans.Any(plan =>
                plan.DependsOnOwnerKeys.Any(dependency =>
                    !validatedIndex.TryGetValue(
                        dependency,
                        out ITenantTerminationContributor? dependencyOwner) ||
                    PhasePlanFor(dependencyOwner.Descriptor, plan.Phase) is null)));
        if (dependencyInvalid)
        {
            index.Clear();
            return false;
        }

        return true;
    }

    private static bool HasDependencyCycle(
        Dictionary<string, ITenantTerminationContributor> index)
    {
        TenantTerminationContributionPhase[] phases = index.Values
            .SelectMany(contributor => contributor.Descriptor.PhasePlans)
            .Select(plan => plan.Phase)
            .Distinct()
            .ToArray();
        return phases.Any(phase => HasDependencyCycle(index, phase));
    }

    private static bool HasDependencyCycle(
        Dictionary<string, ITenantTerminationContributor> index,
        TenantTerminationContributionPhase phase)
    {
        Dictionary<string, TenantTerminationContributorPhasePlan> phasePlans =
            index.Values
                .Select(contributor => new
                {
                    Contributor = contributor,
                    Plan = PhasePlanFor(contributor.Descriptor, phase)
                })
                .Where(item => item.Plan is not null)
                .ToDictionary(
                    item => item.Contributor.Descriptor.OwnerKey,
                    item => item.Plan!,
                    StringComparer.Ordinal);
        Dictionary<string, int> incoming = phasePlans.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.DependsOnOwnerKeys.Count,
            StringComparer.Ordinal);
        Queue<string> ready = new(
            incoming.Where(pair => pair.Value == 0).Select(pair => pair.Key));
        int visited = 0;
        while (ready.Count > 0)
        {
            string ownerKey = ready.Dequeue();
            visited++;
            foreach (KeyValuePair<string, TenantTerminationContributorPhasePlan> candidate in phasePlans)
            {
                if (!candidate.Value.DependsOnOwnerKeys.Contains(
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

        return visited != phasePlans.Count;
    }

    private static bool HasValidPhasePlans(
        TenantTerminationContributorDescriptor descriptor) =>
        descriptor.PhasePlans is not null &&
        descriptor.PhasePlans.Count > 0 &&
        descriptor.PhasePlans.All(plan =>
            plan is not null &&
            Enum.IsDefined(plan.Phase) &&
            plan.Phase != TenantTerminationContributionPhase.Unknown &&
            Enum.IsDefined(plan.ExecutionBoundary) &&
            plan.ExecutionBoundary != TenantTerminationExecutionBoundary.Unknown &&
            (plan.ExecutionBoundary ==
                TenantTerminationExecutionBoundary.TenantScopedTask ||
             plan.Phase == TenantTerminationContributionPhase.Destroy) &&
            plan.DependsOnOwnerKeys is not null &&
            plan.DependsOnOwnerKeys.Count <=
                TenantTerminationContract.MaximumDependencies &&
            plan.DependsOnOwnerKeys.All(ownerKey =>
                IsStableKey(ownerKey) &&
                !string.Equals(
                    ownerKey,
                    descriptor.OwnerKey,
                    StringComparison.Ordinal)) &&
            plan.DependsOnOwnerKeys.Distinct(StringComparer.Ordinal).Count() ==
                plan.DependsOnOwnerKeys.Count) &&
        descriptor.PhasePlans.Select(plan => plan.Phase).Distinct().Count() ==
            descriptor.PhasePlans.Count;

    private static TenantTerminationContributorPhasePlan? PhasePlanFor(
        TenantTerminationContributorDescriptor descriptor,
        TenantTerminationContributionPhase phase) =>
        descriptor.PhasePlans.FirstOrDefault(plan => plan.Phase == phase);

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

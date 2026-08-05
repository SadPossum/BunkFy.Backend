namespace BunkFy.Modules.DataRights.Application.Production;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using BunkFy.Modules.DataRights.Application.Handlers;
using BunkFy.Modules.DataRights.Contracts;
using Gma.Framework.Results;

internal sealed class TenantTerminationProductionCatalog(
    IEnumerable<ITenantTerminationContributor> contributors,
    IEnumerable<ITenantTerminationExportContributor> exportContributors)
    : ITenantTerminationProductionCatalog
{
    private const string DigestDomain =
        "bunkfy.data-rights.tenant-termination.production-catalog.v1";
    private readonly ITenantTerminationContributor[] contributors =
        contributors?.ToArray() ??
        throw new ArgumentNullException(nameof(contributors));
    private readonly ITenantTerminationExportContributor[] exportContributors =
        exportContributors?.ToArray() ??
        throw new ArgumentNullException(nameof(exportContributors));

    public Result<TenantTerminationProductionCatalogEvidence> Validate(
        IReadOnlyCollection<string> requiredOwnerKeys)
    {
        Result catalog = TenantTerminationContributorSet
            .ValidateProductionCatalog(
                this.contributors,
                requiredOwnerKeys);
        if (catalog.IsFailure)
        {
            return Invalid();
        }

        Dictionary<string, ITenantTerminationContributor> owners =
            this.contributors.ToDictionary(
                contributor => contributor.Descriptor.OwnerKey,
                StringComparer.Ordinal);
        string[] required = requiredOwnerKeys
            .Select(ownerKey => ownerKey.Trim())
            .OrderBy(ownerKey => ownerKey, StringComparer.Ordinal)
            .ToArray();
        if (required.Any(ownerKey =>
                !owners.TryGetValue(
                    ownerKey,
                    out ITenantTerminationContributor? contributor) ||
                !contributor.Descriptor.PhasePlans.Any(plan =>
                    plan.Phase ==
                        TenantTerminationContributionPhase.Destroy)))
        {
            return Invalid();
        }

        Result<IReadOnlyList<ITenantTerminationContributor>> destroyOrder =
            TenantTerminationContributorSet.OrderForPhase(
                this.contributors,
                TenantTerminationContributionPhase.Destroy);
        if (destroyOrder.IsFailure ||
            !TryResolveTerminalOwner(
                destroyOrder.Value,
                out string? terminalOwnerKey) ||
            !HasExactControlOwner(
                this.contributors,
                TenantTerminationContributionPhase.Freeze,
                terminalOwnerKey!) ||
            !HasExactControlOwner(
                this.contributors,
                TenantTerminationContributionPhase.Restore,
                terminalOwnerKey!) ||
            !TryIndexExportContributors(
                this.exportContributors,
                owners,
                out Dictionary<string, ITenantTerminationExportContributor>
                    exports))
        {
            return Invalid();
        }

        string[] plannedExportOwners = this.contributors
            .Where(contributor => contributor.Descriptor.PhasePlans.Any(plan =>
                plan.Phase == TenantTerminationContributionPhase.Export))
            .Select(contributor => contributor.Descriptor.OwnerKey)
            .OrderBy(ownerKey => ownerKey, StringComparer.Ordinal)
            .ToArray();
        if (!plannedExportOwners.SequenceEqual(
                exports.Keys.OrderBy(ownerKey => ownerKey, StringComparer.Ordinal),
                StringComparer.Ordinal))
        {
            return Invalid();
        }

        string digest = ComputeDigest(
            required,
            this.contributors,
            exports.Values,
            terminalOwnerKey!);
        return Result.Success(new TenantTerminationProductionCatalogEvidence(
            this.contributors.Length,
            exports.Count,
            terminalOwnerKey!,
            digest));
    }

    private static bool HasExactControlOwner(
        IReadOnlyCollection<ITenantTerminationContributor> contributors,
        TenantTerminationContributionPhase phase,
        string expectedOwnerKey)
    {
        string[] owners = contributors
            .Where(contributor => contributor.Descriptor.PhasePlans.Any(plan =>
                plan.Phase == phase))
            .Select(contributor => contributor.Descriptor.OwnerKey)
            .ToArray();
        return owners.Length == 1 && string.Equals(
            owners[0],
            expectedOwnerKey,
            StringComparison.Ordinal);
    }

    private static bool TryResolveTerminalOwner(
        IReadOnlyCollection<ITenantTerminationContributor> ordered,
        out string? terminalOwnerKey)
    {
        HashSet<string> dependencies = ordered
            .SelectMany(contributor => contributor.Descriptor.PhasePlans
                .Single(plan => plan.Phase ==
                    TenantTerminationContributionPhase.Destroy)
                .DependsOnOwnerKeys)
            .ToHashSet(StringComparer.Ordinal);
        string[] terminalOwners = ordered
            .Select(contributor => contributor.Descriptor.OwnerKey)
            .Where(ownerKey => !dependencies.Contains(ownerKey))
            .ToArray();
        terminalOwnerKey = terminalOwners.Length == 1
            ? terminalOwners[0]
            : null;
        return terminalOwnerKey is not null;
    }

    private static bool TryIndexExportContributors(
        IReadOnlyCollection<ITenantTerminationExportContributor> supplied,
        Dictionary<string, ITenantTerminationContributor> owners,
        out Dictionary<string, ITenantTerminationExportContributor> exports)
    {
        exports = new(StringComparer.Ordinal);
        foreach (ITenantTerminationExportContributor? export in supplied)
        {
            DataRightsExportDescriptor? descriptor = export?.ExportDescriptor;
            if (descriptor is null ||
                !owners.TryGetValue(
                    descriptor.OwnerKey,
                    out ITenantTerminationContributor? owner) ||
                owner.Descriptor.CatalogVersion != descriptor.CatalogVersion ||
                string.IsNullOrWhiteSpace(descriptor.CatalogId) ||
                descriptor.CatalogSchemaVersion <= 0 ||
                string.IsNullOrWhiteSpace(descriptor.ExportSchemaId) ||
                descriptor.ExportSchemaVersion <= 0 ||
                descriptor.FieldIds is null ||
                descriptor.FieldIds.Count == 0 ||
                descriptor.FieldIds.Any(string.IsNullOrWhiteSpace) ||
                descriptor.FieldIds.Distinct(StringComparer.Ordinal).Count() !=
                    descriptor.FieldIds.Count ||
                !exports.TryAdd(descriptor.OwnerKey, export!))
            {
                exports.Clear();
                return false;
            }
        }

        return true;
    }

    private static string ComputeDigest(
        string[] requiredOwnerKeys,
        IReadOnlyCollection<ITenantTerminationContributor> contributors,
        IEnumerable<ITenantTerminationExportContributor> exports,
        string terminalOwnerKey)
    {
        StringBuilder canonical = new();
        Append(canonical, DigestDomain);
        Append(canonical, requiredOwnerKeys.Length);
        foreach (string ownerKey in requiredOwnerKeys)
        {
            Append(canonical, ownerKey);
        }

        ITenantTerminationContributor[] orderedContributors = contributors
            .OrderBy(
                contributor => contributor.Descriptor.OwnerKey,
                StringComparer.Ordinal)
            .ToArray();
        Append(canonical, orderedContributors.Length);
        foreach (ITenantTerminationContributor contributor in
                 orderedContributors)
        {
            TenantTerminationContributorDescriptor descriptor =
                contributor.Descriptor;
            Append(canonical, descriptor.OwnerKey);
            Append(canonical, descriptor.ContractVersion);
            Append(canonical, descriptor.MandatoryForProduction);
            Append(canonical, descriptor.CatalogVersion);
            Append(canonical, descriptor.CatalogSha256);
            TenantTerminationContributorPhasePlan[] plans = descriptor.PhasePlans
                .OrderBy(plan => plan.Phase)
                .ToArray();
            Append(canonical, plans.Length);
            foreach (TenantTerminationContributorPhasePlan plan in plans)
            {
                Append(canonical, (int)plan.Phase);
                Append(canonical, (int)plan.ExecutionBoundary);
                string[] dependencies = plan.DependsOnOwnerKeys
                    .OrderBy(ownerKey => ownerKey, StringComparer.Ordinal)
                    .ToArray();
                Append(canonical, dependencies.Length);
                foreach (string dependency in dependencies)
                {
                    Append(canonical, dependency);
                }
            }
        }

        ITenantTerminationExportContributor[] orderedExports = exports
            .OrderBy(
                export => export.ExportDescriptor.OwnerKey,
                StringComparer.Ordinal)
            .ToArray();
        Append(canonical, orderedExports.Length);
        foreach (ITenantTerminationExportContributor export in orderedExports)
        {
            DataRightsExportDescriptor descriptor = export.ExportDescriptor;
            Append(canonical, descriptor.OwnerKey);
            Append(canonical, descriptor.CatalogId);
            Append(canonical, descriptor.CatalogSchemaVersion);
            Append(canonical, descriptor.CatalogVersion);
            Append(canonical, descriptor.ExportSchemaId);
            Append(canonical, descriptor.ExportSchemaVersion);
            string[] fields = descriptor.FieldIds
                .OrderBy(field => field, StringComparer.Ordinal)
                .ToArray();
            Append(canonical, fields.Length);
            foreach (string field in fields)
            {
                Append(canonical, field);
            }
        }

        Append(canonical, terminalOwnerKey);
        byte[] bytes = Encoding.UTF8.GetBytes(canonical.ToString());
        try
        {
            return Convert.ToHexStringLower(SHA256.HashData(bytes));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    private static void Append(StringBuilder target, string value)
    {
        target.Append(value.Length.ToString(CultureInfo.InvariantCulture));
        target.Append(':');
        target.Append(value);
    }

    private static void Append(StringBuilder target, int value) =>
        Append(target, value.ToString(CultureInfo.InvariantCulture));

    private static void Append(StringBuilder target, bool value) =>
        Append(target, value ? "1" : "0");

    private static Result<TenantTerminationProductionCatalogEvidence> Invalid() =>
        Result.Failure<TenantTerminationProductionCatalogEvidence>(
            DataRightsApplicationErrors
                .TenantTerminationContributorCatalogInvalid);
}

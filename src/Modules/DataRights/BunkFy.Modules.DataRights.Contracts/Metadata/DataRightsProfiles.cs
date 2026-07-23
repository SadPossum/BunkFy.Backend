namespace BunkFy.Modules.DataRights.Contracts;

using Gma.Framework.ModuleComposition;
using Gma.Framework.Tenancy;

public static class DataRightsProfiles
{
    public const string DefaultName = "default";

    public static ModuleProfileDescriptor Default { get; } = new(
        DataRightsModuleMetadata.Name,
        DefaultName,
        provides: [],
        requires:
        [
            new RequiredCompositionFeature(
                TenancyCompositionFeatures.Context,
                Provider,
                reason: "Data-rights cases are tenant-scoped.")
        ],
        displayName: "BunkFy data rights",
        description: "PII-minimal case coordination for controller-managed data-rights workflows.");

    private static string Provider => $"{DataRightsModuleMetadata.Name}/{DefaultName}";
}

namespace BunkFy.Modules.Ingestion.Contracts;

using Gma.Framework.FileManagement;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Tenancy;

public static class IngestionProfiles
{
    public const string DefaultName = "default";

    public static ModuleProfileDescriptor Default { get; } = new(
        IngestionModuleMetadata.Name,
        DefaultName,
        provides:
        [
            IngestionCompositionFeatures.ObservationReceiptsProvided(Provider(DefaultName))
        ],
        requires:
        [
            new RequiredCompositionFeature(
                TenancyCompositionFeatures.Context,
                Provider(DefaultName),
                reason: "Ingestion connections and receipts are tenant-scoped."),
            new RequiredCompositionFeature(
                new CompositionFeatureId(FileManagementCompositionFeatures.Storage),
                Provider(DefaultName),
                reason: "Raw adapter payloads require a configured file-storage adapter such as MinIO.")
        ],
        displayName: "Ingestion default",
        description: "Tenant-scoped external observation receipt and proposal control plane.");

    private static string Provider(string profileName) => $"{IngestionModuleMetadata.Name}/{profileName}";
}

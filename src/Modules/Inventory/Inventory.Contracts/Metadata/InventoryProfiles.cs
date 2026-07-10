namespace Inventory.Contracts;

using Gma.Framework.Messaging;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Tasks;
using Gma.Framework.Tenancy;
using Properties.Contracts;

public static class InventoryProfiles
{
    public const string DefaultName = "default";

    public static ModuleProfileDescriptor Default { get; } = new(
        InventoryModuleMetadata.Name,
        DefaultName,
        provides:
        [
            InventoryCompositionFeatures.AvailabilityProvided(Provider(DefaultName))
        ],
        requires:
        [
            new RequiredCompositionFeature(
                TenancyCompositionFeatures.Context,
                Provider(DefaultName),
                reason: "Inventory is tenant-scoped."),
            PropertiesCompositionFeatures.PhysicalSetupRequired(
                Provider(DefaultName),
                "Inventory copies Properties topology into a local projection."),
            MessagingCompositionFeatures.OutboxRequired(
                Provider(DefaultName),
                "Inventory publishes versioned configuration facts."),
            MessagingCompositionFeatures.NatsConsumersRequired(
                Provider(DefaultName),
                "Live Properties topology projection uses NATS consumers; rebuild remains available.",
                optional: true),
            TasksCompositionFeatures.WorkerRequired(
                Provider(DefaultName),
                "Topology rebuild runs through the task worker.",
                optional: true),
            TasksCompositionFeatures.ScopeContextRequired(
                Provider(DefaultName),
                "Topology rebuild is tenant-scoped.",
                optional: true)
        ],
        requiredModules:
        [
            new RequiredCompositionModule(
                PropertiesModuleMetadata.Name,
                Provider(DefaultName),
                reason: "Properties owns the source topology facts.")
        ],
        displayName: "Inventory default",
        description: "Property-scoped inventory configuration with local Properties topology projections.");

    private static string Provider(string profileName) => $"{InventoryModuleMetadata.Name}/{profileName}";
}

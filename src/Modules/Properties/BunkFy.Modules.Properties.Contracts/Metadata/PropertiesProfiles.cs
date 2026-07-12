namespace BunkFy.Modules.Properties.Contracts;

using Gma.Framework.Messaging;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Tenancy;

public static class PropertiesProfiles
{
    public const string DefaultName = "default";

    public static ModuleProfileDescriptor Default { get; } = new(
        PropertiesModuleMetadata.Name,
        DefaultName,
        provides:
        [
            PropertiesCompositionFeatures.PhysicalSetupProvided(Provider(DefaultName))
        ],
        requires:
        [
            new RequiredCompositionFeature(
                TenancyCompositionFeatures.Context,
                Provider(DefaultName),
                reason: "Properties is tenant-scoped; register TenancyModule or baseline tenancy infrastructure."),
            MessagingCompositionFeatures.OutboxRequired(
                Provider(DefaultName),
                "Properties publishes integration events through its module outbox; register Gma.Framework.Messaging.Infrastructure.")
        ],
        displayName: "Properties default",
        description: "Tenant-scoped property, room, and bed setup with producer-owned outbox events.");

    private static string Provider(string profileName) => $"{PropertiesModuleMetadata.Name}/{profileName}";
}

namespace BunkFy.Modules.Guests.Contracts;

using Gma.Framework.Messaging;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Tasks;
using Gma.Framework.Tenancy;
using BunkFy.Modules.Properties.Contracts;

public static class GuestsProfiles
{
    public const string DefaultName = "default";

    public static ModuleProfileDescriptor Default { get; } = new(
        GuestsModuleMetadata.Name,
        DefaultName,
        provides: [GuestsCompositionFeatures.RecordsProvided(Provider(DefaultName))],
        requires:
        [
            new RequiredCompositionFeature(
                TenancyCompositionFeatures.Context,
                Provider(DefaultName),
                reason: "Guest records are tenant scoped."),
            MessagingCompositionFeatures.OutboxRequired(
                Provider(DefaultName),
                "Privacy-safe guest lifecycle facts use the Guests outbox."),
            MessagingCompositionFeatures.NatsConsumersRequired(
                Provider(DefaultName),
                "Property lifecycle facts maintain Guests' local property projection.",
                optional: true),
            TasksCompositionFeatures.WorkerRequired(
                Provider(DefaultName),
                "Property projection rebuilds run through the task worker.",
                optional: true),
            TasksCompositionFeatures.ScopeContextRequired(
                Provider(DefaultName),
                "Property projection rebuilds are tenant scoped.",
                optional: true)
        ],
        requiredModules:
        [
            new RequiredCompositionModule(
                PropertiesModuleMetadata.Name,
                Provider(DefaultName),
                reason: "Guest visibility begins at a managed property.")
        ],
        displayName: "Guests default",
        description: "Staff-managed canonical guest profiles with property-scoped visibility.");

    private static string Provider(string profileName) => $"{GuestsModuleMetadata.Name}/{profileName}";
}

public static class GuestsCompositionFeatures
{
    public static readonly CompositionFeatureId Records = new("guests.records");

    public static ProvidedCompositionFeature RecordsProvided(string provider) =>
        new(Records, provider, "Guest record management is selected.");
}

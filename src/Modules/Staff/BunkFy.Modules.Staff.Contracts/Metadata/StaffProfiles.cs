namespace BunkFy.Modules.Staff.Contracts;

using Gma.Framework.Messaging;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Tasks;
using Gma.Framework.Tenancy;
using BunkFy.Modules.Properties.Contracts;

public static class StaffProfiles
{
    public const string DefaultName = "default";

    public static ModuleProfileDescriptor Default { get; } = new(
        StaffModuleMetadata.Name,
        DefaultName,
        provides: [StaffCompositionFeatures.ProfilesProvided(Provider(DefaultName))],
        requires:
        [
            new RequiredCompositionFeature(TenancyCompositionFeatures.Context, Provider(DefaultName),
                reason: "Staff profiles are tenant scoped."),
            MessagingCompositionFeatures.OutboxRequired(Provider(DefaultName),
                "Staff lifecycle and assignment facts use the Staff outbox."),
            MessagingCompositionFeatures.NatsConsumersRequired(Provider(DefaultName),
                "Property lifecycle facts maintain Staff's local property projection.", optional: true),
            TasksCompositionFeatures.WorkerRequired(Provider(DefaultName),
                "Property projection rebuilds run through the task worker.", optional: true),
            TasksCompositionFeatures.ScopeContextRequired(Provider(DefaultName),
                "Property projection rebuilds are tenant scoped.", optional: true)
        ],
        requiredModules:
        [
            new RequiredCompositionModule(PropertiesModuleMetadata.Name, Provider(DefaultName),
                reason: "Staff work assignments refer to managed properties.")
        ],
        displayName: "Staff default",
        description: "Operator employment profiles and property work assignments.");

    private static string Provider(string profileName) => $"{StaffModuleMetadata.Name}/{profileName}";
}

public static class StaffCompositionFeatures
{
    public static readonly CompositionFeatureId Profiles = new("staff.profiles");

    public static ProvidedCompositionFeature ProfilesProvided(string provider) =>
        new(Profiles, provider, "Staff profile management is selected.");
}

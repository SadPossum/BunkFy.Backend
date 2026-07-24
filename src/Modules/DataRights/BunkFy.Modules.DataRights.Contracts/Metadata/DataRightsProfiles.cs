namespace BunkFy.Modules.DataRights.Contracts;

using Gma.Framework.ModuleComposition;
using Gma.Framework.Messaging;
using Gma.Framework.Tasks;
using Gma.Framework.Tenancy;
using BunkFy.Modules.Properties.Contracts;

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
                reason: "Data-rights cases are tenant-scoped."),
            MessagingCompositionFeatures.NatsConsumersRequired(
                Provider,
                "Property lifecycle facts maintain DataRights' local governance projection.",
                optional: true),
            TasksCompositionFeatures.WorkerRequired(
                Provider,
                "Property governance projection rebuilds run through the task worker.",
                optional: true),
            TasksCompositionFeatures.ScopeContextRequired(
                Provider,
                "Property governance projection rebuilds are tenant scoped.",
                optional: true)
        ],
        requiredModules:
        [
            new RequiredCompositionModule(
                PropertiesModuleMetadata.Name,
                Provider,
                reason: "Destructive approvals require property-owned governance evidence.")
        ],
        displayName: "BunkFy data rights",
        description: "PII-minimal case coordination for controller-managed data-rights workflows.");

    private static string Provider => $"{DataRightsModuleMetadata.Name}/{DefaultName}";
}

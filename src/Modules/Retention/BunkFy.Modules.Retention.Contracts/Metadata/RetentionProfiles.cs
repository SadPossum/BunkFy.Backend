namespace BunkFy.Modules.Retention.Contracts;

using BunkFy.Modules.Properties.Contracts;
using Gma.Framework.Messaging;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Tasks;
using Gma.Framework.Tenancy;
using Gma.Modules.Organizations.Contracts;

public static class RetentionProfiles
{
    public const string DefaultName = "default";

    public static ModuleProfileDescriptor Default { get; } = new(
        RetentionModuleMetadata.Name,
        DefaultName,
        provides: [],
        requires:
        [
            new RequiredCompositionFeature(
                TenancyCompositionFeatures.Context,
                Provider,
                reason: "Retention schedules and executions are tenant scoped."),
            MessagingCompositionFeatures.NatsConsumersRequired(
                Provider,
                "Organization and property facts maintain Retention-owned scope projections.",
                optional: true),
            TasksCompositionFeatures.WorkerRequired(
                Provider,
                "Retention contributors execute through the durable task worker.",
                optional: true),
            TasksCompositionFeatures.ScopeContextRequired(
                Provider,
                "Retention executions are fenced to one tenant.",
                optional: true)
        ],
        requiredModules:
        [
            new RequiredCompositionModule(
                OrganizationsModuleMetadata.Name,
                Provider,
                reason: "Active workspaces define tenant-scoped retention schedules."),
            new RequiredCompositionModule(
                PropertiesModuleMetadata.Name,
                Provider,
                reason: "Processing-enabled properties define property-scoped retention schedules.")
        ],
        displayName: "BunkFy retention",
        description: "PII-minimized scheduling and evidence for owner-module retention work.");

    private static string Provider =>
        $"{RetentionModuleMetadata.Name}/{DefaultName}";
}

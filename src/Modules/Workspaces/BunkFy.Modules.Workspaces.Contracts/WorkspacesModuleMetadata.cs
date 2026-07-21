namespace BunkFy.Modules.Workspaces.Contracts;

using BunkFy.Modules.Staff.Contracts;
using Gma.Framework.Messaging;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Modules;
using Gma.Modules.AccessControl.Contracts;
using Gma.Modules.Auth.Contracts;
using Gma.Modules.Organizations.Contracts;

public static class WorkspacesModuleMetadata
{
    public const string Name = "workspaces";
    public const string Schema = "workspaces";
    public const string InvitationChangedHandlerName = "staff-invitation-changed";
    public const string EnrollmentClaimChangedHandlerName = "staff-enrollment-claim-changed";
    public const string EnrollmentLinkChangedHandlerName = "staff-enrollment-link-changed";
    public const string MembershipAccessSeedHandlerName = "bunkfy-workspace-access-profile-seeds";
    public const string StaffAccessLifecycleHandlerName = "bunkfy-workspace-staff-access-lifecycle";

    public static ModuleDescriptor Descriptor { get; } = ModuleDescriptor
        .Create(Name)
        .WithSchema(Schema)
        .WithSubscription<OrganizationInvitationChangedIntegrationEvent>(
            OrganizationsModuleMetadata.Name,
            InvitationChangedHandlerName)
        .WithSubscription<OrganizationEnrollmentClaimChangedIntegrationEvent>(
            OrganizationsModuleMetadata.Name,
            EnrollmentClaimChangedHandlerName)
        .WithSubscription<OrganizationEnrollmentLinkChangedIntegrationEvent>(
            OrganizationsModuleMetadata.Name,
            EnrollmentLinkChangedHandlerName)
        .WithSubscription<OrganizationMembershipChangedIntegrationEvent>(
            OrganizationsModuleMetadata.Name,
            MembershipAccessSeedHandlerName)
        .WithSubscription<StaffMemberLifecycleChangedIntegrationEvent>(
            StaffModuleMetadata.Name,
            StaffAccessLifecycleHandlerName)
        .WithProfile(WorkspacesProfiles.Default)
        .Build();
}

public static class WorkspacesProfiles
{
    public const string DefaultName = "default";

    public static ModuleProfileDescriptor Default { get; } = new(
        WorkspacesModuleMetadata.Name,
        DefaultName,
        provides: [],
        requires:
        [
            MessagingCompositionFeatures.NatsConsumersRequired(
                Provider,
                "Organizations join facts drive durable BunkFy Staff provisioning.",
                optional: true)
        ],
        requiredModules:
        [
            new RequiredCompositionModule(AuthModuleMetadata.Name, Provider,
                reason: "Applicant identity and verified email are Auth-owned."),
            new RequiredCompositionModule(OrganizationsModuleMetadata.Name, Provider,
                reason: "Invitations, enrollment claims, and memberships are Organizations-owned."),
            new RequiredCompositionModule(AccessControlModuleMetadata.Name, Provider,
                reason: "Completed onboarding installs a constrained workspace assignment."),
            new RequiredCompositionModule(StaffModuleMetadata.Name, Provider,
                reason: "Accepted applicant profiles become Staff-owned employment records.")
        ],
        displayName: "BunkFy workspaces",
        description: "Product-owned Staff enrollment and workspace provisioning coordination.");

    private static string Provider => $"{WorkspacesModuleMetadata.Name}/{DefaultName}";
}

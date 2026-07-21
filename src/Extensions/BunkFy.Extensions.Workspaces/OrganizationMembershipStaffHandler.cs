namespace BunkFy.Extensions.Workspaces;

using BunkFy.Modules.Staff.Contracts;
using Gma.Framework.Messaging;
using Gma.Modules.Auth.Contracts;
using Gma.Modules.Organizations.Contracts;
using Microsoft.Extensions.Options;

[IntegrationEventHandler(HandlerName, RequiresExplicitProducerBinding = true)]
internal sealed class OrganizationMembershipStaffHandler(
    IStaffIdentityReconciler staff,
    IAuthMemberContactReader contacts,
    IOptions<BunkFyWorkspacesOptions> options)
    : IIntegrationEventHandler<OrganizationMembershipChangedIntegrationEvent>
{
    public const string HandlerName = "bunkfy-workspace-staff-membership";

    public async Task HandleAsync(
        OrganizationMembershipChangedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        if (integrationEvent.Status == OrganizationMembershipStatus.Active &&
            integrationEvent.Role != OrganizationMembershipRole.Owner)
        {
            return;
        }

        string? verifiedEmail = await this.GetVerifiedEmailAsync(
            integrationEvent.SubjectId,
            cancellationToken).ConfigureAwait(false);
        string displayName = verifiedEmail ?? DefaultDisplayName(integrationEvent.SubjectId);
        StaffIdentityReconciliationResult result = await staff.ReconcileAsync(
            new StaffIdentityReconciliationRequest(
                integrationEvent.SubjectId,
                displayName,
                verifiedEmail,
                integrationEvent.Status == OrganizationMembershipStatus.Active,
                "integration:organizations",
                "Organization membership changed."),
            cancellationToken).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            throw new InvalidOperationException(
                $"Staff identity reconciliation failed with '{result.ErrorCode}'.");
        }
    }

    private async Task<string?> GetVerifiedEmailAsync(
        string subjectId,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(subjectId, out Guid memberId))
        {
            return null;
        }

        return await contacts.GetPreferredVerifiedEmailAsync(
            options.Value.GlobalAuthScopeId,
            memberId,
            cancellationToken).ConfigureAwait(false);
    }

    private static string DefaultDisplayName(string subjectId)
    {
        const int visibleCharacters = 12;
        string suffix = subjectId.Length <= visibleCharacters
            ? subjectId
            : subjectId[..visibleCharacters];
        return $"Workspace member {suffix}";
    }
}

namespace BunkFy.Modules.Retention.Application.Handlers;

using BunkFy.Modules.Retention.Application.Ports;
using Gma.Framework.Scoping;

internal sealed class RetentionScopeMutationCoordinator(
    IRetentionMutationLock mutationLock,
    IRetentionScopeRepository scopes,
    IScopeContext scopeContext)
{
    public async Task ApplyOrganizationAsync(
        RetentionOrganizationWriteModel organization,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(organization);
        string tenantId = this.RequireTenant(organization.ScopeId);
        await mutationLock.AcquireTenantTargetWriteAsync(
                tenantId,
                cancellationToken)
            .ConfigureAwait(false);
        await scopes.ApplyOrganizationAsync(organization, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task ApplyPropertyTopologyAsync(
        RetentionPropertyTopologyWriteModel property,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(property);
        string tenantId = this.RequireTenant(property.ScopeId);
        await mutationLock.AcquirePropertyTargetWriteAsync(
                tenantId,
                property.PropertyId,
                cancellationToken)
            .ConfigureAwait(false);
        await scopes.ApplyPropertyTopologyAsync(property, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task ApplyPropertyPolicyAsync(
        RetentionPropertyPolicyWriteModel property,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(property);
        string tenantId = this.RequireTenant(property.ScopeId);
        await mutationLock.AcquirePropertyTargetWriteAsync(
                tenantId,
                property.PropertyId,
                cancellationToken)
            .ConfigureAwait(false);
        await scopes.ApplyPropertyPolicyAsync(property, cancellationToken)
            .ConfigureAwait(false);
    }

    private string RequireTenant(string expectedTenantId)
    {
        string tenantId = scopeContext.IsEnabled
            ? scopeContext.ScopeId?.Trim() ?? string.Empty
            : string.Empty;
        if (tenantId.Length == 0 ||
            !string.Equals(
                tenantId,
                expectedTenantId?.Trim(),
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "A Retention scope mutation requires the current tenant scope.");
        }

        return tenantId;
    }
}

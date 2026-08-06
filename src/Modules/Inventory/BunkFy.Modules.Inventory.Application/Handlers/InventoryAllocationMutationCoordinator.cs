namespace BunkFy.Modules.Inventory.Application.Handlers;

using BunkFy.Modules.Inventory.Application.Ports;
using BunkFy.Modules.Inventory.Domain.Aggregates;
using Gma.Framework.Scoping;

internal sealed class InventoryAllocationMutationCoordinator(
    IInventoryAllocationOperationLock operationLock,
    IScopeContext scopeContext)
{
    public Task<InventoryAllocation?> AcquireExistingAsync(
        Guid allocationId,
        Func<CancellationToken, Task<InventoryAllocation?>> authoritativeReload,
        CancellationToken cancellationToken) => this.AcquireAndReloadAsync(
        allocationId,
        authoritativeReload,
        cancellationToken);

    public async Task<bool> AcquireRestoreCoordinateAsync(
        Guid allocationId,
        CancellationToken cancellationToken)
    {
        if (!this.TryGetTenantId(allocationId, out string tenantId))
        {
            return false;
        }

        await operationLock.AcquireCoordinateAsync(
            tenantId,
            allocationId,
            cancellationToken).ConfigureAwait(false);
        return true;
    }

    private async Task<InventoryAllocation?> AcquireAndReloadAsync(
        Guid allocationId,
        Func<CancellationToken, Task<InventoryAllocation?>> reload,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reload);
        if (!this.TryGetTenantId(allocationId, out string tenantId) ||
            !await operationLock.TryAcquireExistingAsync(
                tenantId,
                allocationId,
                cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        InventoryAllocation? reloaded = await reload(cancellationToken)
            .ConfigureAwait(false);
        return reloaded is not null &&
            reloaded.Id == allocationId &&
            string.Equals(
                reloaded.ScopeId,
                tenantId,
                StringComparison.Ordinal)
                ? reloaded
                : null;
    }

    private bool TryGetTenantId(
        Guid allocationId,
        out string tenantId)
    {
        tenantId = scopeContext.IsEnabled
            ? scopeContext.ScopeId?.Trim() ?? string.Empty
            : string.Empty;
        return allocationId != Guid.Empty && tenantId.Length > 0;
    }
}

namespace BunkFy.Modules.Inventory.Persistence;

using BunkFy.Modules.Inventory.Persistence.TenantTermination;
using Gma.Framework.Domain;
using Gma.Framework.Naming;

internal sealed class InventoryTenantRevision : IScopedEntity
{
    private InventoryTenantRevision() { }

    private InventoryTenantRevision(string scopeId)
    {
        this.ScopeId = scopeId;
        this.Revision = 1;
        this.LifecycleStatus = InventoryTenantLifecycleStatus.Open;
    }

    public string ScopeId { get; private set; } = string.Empty;

    public long Revision { get; private set; }

    public InventoryTenantLifecycleStatus LifecycleStatus { get; private set; }

    public Guid? DestroyOperationId { get; private set; }

    public string? DestroyRequestSha256 { get; private set; }

    public DateTimeOffset? DestroyStartedAtUtc { get; private set; }

    public DateTimeOffset? DestroyCompletedAtUtc { get; private set; }

    public bool IsOpen =>
        this.LifecycleStatus == InventoryTenantLifecycleStatus.Open;

    public static InventoryTenantRevision Create(string tenantId) =>
        new(TenantIds.Normalize(tenantId));

    public void Advance()
    {
        if (!this.IsOpen)
        {
            throw new InvalidOperationException(
                "A closing Inventory tenant cannot advance its revision.");
        }

        this.Revision = checked(this.Revision + 1);
    }

    public static InventoryTenantRevision? TryBeginClosing(
        string tenantId,
        Guid operationId,
        string requestSha256,
        DateTimeOffset startedAtUtc)
    {
        InventoryTenantRevision revision = Create(tenantId);
        if (operationId == Guid.Empty ||
            !InventoryTenantLifecycleHashes.IsSha256(requestSha256) ||
            startedAtUtc == default)
        {
            return null;
        }

        revision.LifecycleStatus = InventoryTenantLifecycleStatus.Closing;
        revision.DestroyOperationId = operationId;
        revision.DestroyRequestSha256 = requestSha256;
        revision.DestroyStartedAtUtc = startedAtUtc;
        return revision;
    }

    public bool BeginClosing(
        Guid operationId,
        string requestSha256,
        long selectedRevision,
        DateTimeOffset startedAtUtc)
    {
        if (!this.IsOpen ||
            operationId == Guid.Empty ||
            !InventoryTenantLifecycleHashes.IsSha256(requestSha256) ||
            selectedRevision != this.Revision ||
            selectedRevision == long.MaxValue ||
            startedAtUtc == default)
        {
            return false;
        }

        this.Revision = selectedRevision + 1;
        this.LifecycleStatus = InventoryTenantLifecycleStatus.Closing;
        this.DestroyOperationId = operationId;
        this.DestroyRequestSha256 = requestSha256;
        this.DestroyStartedAtUtc = startedAtUtc;
        return true;
    }

    public bool Matches(Guid operationId, string requestSha256) =>
        this.DestroyOperationId == operationId &&
        string.Equals(
            this.DestroyRequestSha256,
            requestSha256,
            StringComparison.Ordinal);

    public bool CompleteDestruction(DateTimeOffset completedAtUtc)
    {
        if (this.LifecycleStatus != InventoryTenantLifecycleStatus.Closing ||
            this.DestroyStartedAtUtc is not DateTimeOffset startedAtUtc ||
            completedAtUtc < startedAtUtc)
        {
            return false;
        }

        this.LifecycleStatus = InventoryTenantLifecycleStatus.Closed;
        this.DestroyCompletedAtUtc = completedAtUtc;
        return true;
    }
}

internal enum InventoryTenantLifecycleStatus
{
    Unknown = 0,
    Open = 1,
    Closing = 2,
    Closed = 3
}

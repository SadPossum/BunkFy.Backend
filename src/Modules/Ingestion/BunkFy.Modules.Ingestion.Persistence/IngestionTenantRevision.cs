namespace BunkFy.Modules.Ingestion.Persistence;

using BunkFy.Modules.Ingestion.Persistence.TenantTermination;
using Gma.Framework.Domain;
using Gma.Framework.Naming;

internal sealed class IngestionTenantRevision : IScopedEntity
{
    private IngestionTenantRevision()
    {
    }

    private IngestionTenantRevision(string scopeId)
    {
        this.ScopeId = scopeId;
        this.Revision = 1;
        this.LifecycleStatus = IngestionTenantLifecycleStatus.Open;
    }

    public string ScopeId { get; private set; } = string.Empty;

    public long Revision { get; private set; }

    public IngestionTenantLifecycleStatus LifecycleStatus { get; private set; }

    public Guid? DestroyOperationId { get; private set; }

    public string? DestroyRequestSha256 { get; private set; }

    public DateTimeOffset? DestroyStartedAtUtc { get; private set; }

    public DateTimeOffset? DestroyCompletedAtUtc { get; private set; }

    public bool IsOpen =>
        this.LifecycleStatus == IngestionTenantLifecycleStatus.Open;

    public static IngestionTenantRevision Create(string tenantId)
    {
        if (!TenantIds.TryNormalize(tenantId, out string? scopeId))
        {
            throw new ArgumentException(
                "The tenant identifier is invalid.",
                nameof(tenantId));
        }

        return new IngestionTenantRevision(scopeId);
    }

    public void Advance()
    {
        if (!this.IsOpen)
        {
            throw new InvalidOperationException(
                "A closing Ingestion tenant cannot advance its revision.");
        }

        this.Revision = checked(this.Revision + 1);
    }

    public static IngestionTenantRevision? TryBeginClosing(
        string tenantId,
        Guid operationId,
        string requestSha256,
        DateTimeOffset startedAtUtc)
    {
        IngestionTenantRevision revision = Create(tenantId);
        if (operationId == Guid.Empty ||
            !IngestionTenantLifecycleHashes.IsSha256(requestSha256) ||
            startedAtUtc == default)
        {
            return null;
        }

        revision.LifecycleStatus = IngestionTenantLifecycleStatus.Closing;
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
            !IngestionTenantLifecycleHashes.IsSha256(requestSha256) ||
            selectedRevision != this.Revision ||
            selectedRevision == long.MaxValue ||
            startedAtUtc == default)
        {
            return false;
        }

        this.Revision = selectedRevision + 1;
        this.LifecycleStatus = IngestionTenantLifecycleStatus.Closing;
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
        if (this.LifecycleStatus != IngestionTenantLifecycleStatus.Closing ||
            this.DestroyStartedAtUtc is not DateTimeOffset startedAtUtc ||
            completedAtUtc < startedAtUtc)
        {
            return false;
        }

        this.LifecycleStatus = IngestionTenantLifecycleStatus.Closed;
        this.DestroyCompletedAtUtc = completedAtUtc;
        return true;
    }
}

internal enum IngestionTenantLifecycleStatus
{
    Unknown = 0,
    Open = 1,
    Closing = 2,
    Closed = 3
}

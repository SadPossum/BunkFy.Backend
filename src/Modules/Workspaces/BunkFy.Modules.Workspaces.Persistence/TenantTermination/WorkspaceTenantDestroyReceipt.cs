namespace BunkFy.Modules.Workspaces.Persistence.TenantTermination;

using Gma.Framework.Domain;

internal sealed class WorkspaceTenantDestroyReceipt : IScopedEntity
{
    private WorkspaceTenantDestroyReceipt() { }

    private WorkspaceTenantDestroyReceipt(
        WorkspaceTenantDestroyOperation operation,
        Guid closeFenceReceiptId,
        DateTimeOffset completedAtUtc)
    {
        this.OperationId = operation.OperationId;
        this.ScopeId = operation.ScopeId;
        this.RequestSha256 = operation.RequestSha256;
        this.FenceId = operation.FenceId;
        this.CloseFenceReceiptId = closeFenceReceiptId;
        this.SelectedFenceVersion = operation.SelectedFenceVersion;
        this.ResultingFenceVersion = operation.ResultingFenceVersion;
        this.BatchSize = operation.BatchSize;
        this.RemovedRecordCount = operation.RemovedRecordCount;
        this.CompletedBatchCount = operation.CompletedBatchCount;
        this.RemovalProofVersion = operation.ProofVersion;
        this.RemovalProofSha256 = operation.RemovalProofSha256;
        this.StartedAtUtc = operation.StartedAtUtc;
        this.CompletedAtUtc = completedAtUtc;
    }

    public Guid OperationId { get; private set; }
    public string ScopeId { get; private set; } = string.Empty;
    public string RequestSha256 { get; private set; } = string.Empty;
    public Guid FenceId { get; private set; }
    public Guid CloseFenceReceiptId { get; private set; }
    public long SelectedFenceVersion { get; private set; }
    public long ResultingFenceVersion { get; private set; }
    public int BatchSize { get; private set; }
    public long RemovedRecordCount { get; private set; }
    public int CompletedBatchCount { get; private set; }
    public int RemovalProofVersion { get; private set; }
    public string RemovalProofSha256 { get; private set; } = string.Empty;
    public DateTimeOffset StartedAtUtc { get; private set; }
    public DateTimeOffset CompletedAtUtc { get; private set; }

    public static WorkspaceTenantDestroyReceipt? TryCreate(
        WorkspaceTenantDestroyOperation operation,
        Guid closeFenceReceiptId,
        DateTimeOffset completedAtUtc)
    {
        bool progressShapeValid = operation is not null &&
            ((operation.RemovedRecordCount == 0 &&
              operation.CompletedBatchCount == 0) ||
             (operation.RemovedRecordCount > 0 &&
              operation.CompletedBatchCount > 0));
        if (operation is null ||
            !operation.IsComplete ||
            closeFenceReceiptId == Guid.Empty ||
            !progressShapeValid ||
            operation.ResultingFenceVersion !=
                operation.SelectedFenceVersion + 2 ||
            operation.ProofVersion !=
                WorkspaceTenantDestroyOperation.RemovalProofVersion ||
            completedAtUtc < operation.UpdatedAtUtc)
        {
            return null;
        }

        return new(operation, closeFenceReceiptId, completedAtUtc);
    }

    public bool Matches(Guid operationId, string requestSha256) =>
        this.OperationId == operationId &&
        string.Equals(
            this.RequestSha256,
            requestSha256,
            StringComparison.Ordinal);
}

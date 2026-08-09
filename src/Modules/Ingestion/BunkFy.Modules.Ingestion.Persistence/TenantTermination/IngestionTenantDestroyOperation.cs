namespace BunkFy.Modules.Ingestion.Persistence.TenantTermination;

using Gma.Framework.Domain;

internal sealed class IngestionTenantDestroyOperation : IScopedEntity
{
    public const int MaximumBatchSize = 500;
    public const int RemovalProofVersion = 1;
    public const int RawPayloadProofVersion = 1;
    public static readonly string InitialRemovalProofSha256 =
        IngestionTenantLifecycleHashes.Sha256(
            "bunkfy-ingestion-tenant-destroy-proof/v1|empty");
    public static readonly string InitialRawPayloadProofSha256 =
        IngestionTenantLifecycleHashes.Sha256(
            "bunkfy-ingestion-tenant-destroy-raw-payload-proof/v1|empty");

    private IngestionTenantDestroyOperation() { }

    private IngestionTenantDestroyOperation(
        Guid operationId,
        string scopeId,
        string requestSha256,
        long selectedRevision,
        int batchSize,
        DateTimeOffset startedAtUtc)
    {
        this.OperationId = operationId;
        this.ScopeId = scopeId;
        this.RequestSha256 = requestSha256;
        this.SelectedRevision = selectedRevision;
        this.ResultingRevision = selectedRevision + 1;
        this.BatchSize = batchSize;
        this.Stage = IngestionTenantDestroyStage.RawPayloadObjects;
        this.RemovalProofSha256 = InitialRemovalProofSha256;
        this.RawPayloadRemovalProofSha256 = InitialRawPayloadProofSha256;
        this.StartedAtUtc = startedAtUtc;
        this.UpdatedAtUtc = startedAtUtc;
        this.ConcurrencyVersion = 1;
    }

    public Guid OperationId { get; private set; }
    public string ScopeId { get; private set; } = string.Empty;
    public string RequestSha256 { get; private set; } = string.Empty;
    public long SelectedRevision { get; private set; }
    public long ResultingRevision { get; private set; }
    public int BatchSize { get; private set; }
    public IngestionTenantDestroyStage Stage { get; private set; }
    public long RemovedRecordCount { get; private set; }
    public int CompletedBatchCount { get; private set; }
    public int ProofVersion { get; private set; } = RemovalProofVersion;
    public string RemovalProofSha256 { get; private set; } = string.Empty;
    public long RemovedRawPayloadCount { get; private set; }
    public int CompletedRawPayloadBatchCount { get; private set; }
    public int RawPayloadRemovalProofVersion { get; private set; } =
        RawPayloadProofVersion;
    public string RawPayloadRemovalProofSha256 { get; private set; } =
        string.Empty;
    public DateTimeOffset StartedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public int ConcurrencyVersion { get; private set; }
    public bool IsComplete =>
        this.Stage == IngestionTenantDestroyStage.Completed;

    public static IngestionTenantDestroyOperation? TryCreate(
        Guid operationId,
        string scopeId,
        string requestSha256,
        long selectedRevision,
        int batchSize,
        DateTimeOffset startedAtUtc)
    {
        if (operationId == Guid.Empty ||
            string.IsNullOrWhiteSpace(scopeId) ||
            !IngestionTenantLifecycleHashes.IsSha256(requestSha256) ||
            selectedRevision is < 0 or long.MaxValue ||
            batchSize is < 1 or > MaximumBatchSize ||
            startedAtUtc == default)
        {
            return null;
        }

        return new(
            operationId,
            scopeId,
            requestSha256,
            selectedRevision,
            batchSize,
            startedAtUtc);
    }

    public bool Matches(Guid operationId, string requestSha256) =>
        this.OperationId == operationId &&
        string.Equals(
            this.RequestSha256,
            requestSha256,
            StringComparison.Ordinal);

    public bool RecordRawPayloadBatch(
        int removedPayloadCount,
        string removedPayloadKeysSha256,
        bool stageCompleted,
        DateTimeOffset recordedAtUtc)
    {
        if (this.Stage != IngestionTenantDestroyStage.RawPayloadObjects ||
            removedPayloadCount is < 1 ||
            removedPayloadCount > this.BatchSize ||
            !IngestionTenantLifecycleHashes.IsSha256(
                removedPayloadKeysSha256) ||
            recordedAtUtc < this.UpdatedAtUtc ||
            this.RemovedRawPayloadCount > long.MaxValue - removedPayloadCount ||
            this.CompletedRawPayloadBatchCount == int.MaxValue ||
            this.ConcurrencyVersion == int.MaxValue)
        {
            return false;
        }

        int nextBatch = this.CompletedRawPayloadBatchCount + 1;
        this.RawPayloadRemovalProofSha256 =
            IngestionTenantLifecycleHashes.Sha256(
                "bunkfy-ingestion-tenant-destroy-raw-payload-proof/v1|" +
                $"{this.RawPayloadRemovalProofSha256}|{nextBatch}|" +
                $"{removedPayloadCount}|{removedPayloadKeysSha256}");
        this.RemovedRawPayloadCount += removedPayloadCount;
        this.CompletedRawPayloadBatchCount = nextBatch;
        this.UpdatedAtUtc = recordedAtUtc;
        this.ConcurrencyVersion++;
        if (stageCompleted)
        {
            this.Stage = IngestionTenantDestroyStage.OutboxMessages;
        }

        return true;
    }

    public bool RecordBatch(
        IngestionTenantDestroyStage stage,
        int removedRecordCount,
        string removedRecordKeysSha256,
        bool stageCompleted,
        DateTimeOffset recordedAtUtc)
    {
        if (this.IsComplete ||
            stage != this.Stage ||
            stage == IngestionTenantDestroyStage.RawPayloadObjects ||
            removedRecordCount is < 1 ||
            removedRecordCount > this.BatchSize ||
            !IngestionTenantLifecycleHashes.IsSha256(
                removedRecordKeysSha256) ||
            recordedAtUtc < this.UpdatedAtUtc ||
            this.RemovedRecordCount > long.MaxValue - removedRecordCount ||
            this.CompletedBatchCount == int.MaxValue ||
            this.ConcurrencyVersion == int.MaxValue)
        {
            return false;
        }

        int nextBatch = this.CompletedBatchCount + 1;
        this.RemovalProofSha256 = IngestionTenantLifecycleHashes.Sha256(
            "bunkfy-ingestion-tenant-destroy-proof/v1|" +
            $"{this.RemovalProofSha256}|{nextBatch}|{(int)stage}|" +
            $"{removedRecordCount}|{removedRecordKeysSha256}");
        this.RemovedRecordCount += removedRecordCount;
        this.CompletedBatchCount = nextBatch;
        this.UpdatedAtUtc = recordedAtUtc;
        this.ConcurrencyVersion++;
        if (stageCompleted)
        {
            this.Stage = Next(stage);
        }

        return true;
    }

    public bool AdvanceEmptyStage(DateTimeOffset recordedAtUtc)
    {
        if (this.IsComplete ||
            recordedAtUtc < this.UpdatedAtUtc ||
            this.ConcurrencyVersion == int.MaxValue)
        {
            return false;
        }

        this.Stage = Next(this.Stage);
        this.UpdatedAtUtc = recordedAtUtc;
        this.ConcurrencyVersion++;
        return true;
    }

    private static IngestionTenantDestroyStage Next(
        IngestionTenantDestroyStage stage)
    {
        if (stage is < IngestionTenantDestroyStage.RawPayloadObjects or
            >= IngestionTenantDestroyStage.Completed)
        {
            throw new InvalidOperationException(
                "The Ingestion tenant destruction stage is invalid.");
        }

        return (IngestionTenantDestroyStage)((int)stage + 1);
    }
}

internal enum IngestionTenantDestroyStage
{
    Unknown = 0,
    RawPayloadObjects = 1,
    OutboxMessages = 2,
    InboxMessages = 3,
    ReservationDispatches = 4,
    ChangeProposals = 5,
    ObservationReprocessingOutputs = 6,
    ObservationReprocessingGraph = 7,
    SourceObservationReceipts = 8,
    AnonymisationReceipts = 9,
    AnonymisationFingerprints = 10,
    AnonymisationRecordPlan = 11,
    AnonymisationTombstones = 12,
    ReservationSourceLinks = 13,
    Runs = 14,
    AdapterIngressCredentials = 15,
    LegalHolds = 16,
    RetentionExecutions = 17,
    ConnectionManagementOperations = 18,
    AdapterConnections = 19,
    AdapterIngressTenantControls = 20,
    PropertyProjections = 21,
    ProjectionRebuildCheckpoints = 22,
    Completed = 23
}

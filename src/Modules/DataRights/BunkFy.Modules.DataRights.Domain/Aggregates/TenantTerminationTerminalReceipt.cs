namespace BunkFy.Modules.DataRights.Domain.Aggregates;

using BunkFy.Modules.DataRights.Domain.Errors;
using Gma.Framework.Domain.Models;
using Gma.Framework.Naming;
using Gma.Framework.Results;

public sealed class TenantTerminationTerminalReceipt
    : ScopedAggregateRoot<Guid>
{
    public const int OwnerKeyMaxLength =
        TenantTerminationOwnerWorkItem.OwnerKeyMaxLength;
    public const int Sha256Length = TenantTerminationProcess.Sha256Length;
    public const int MaximumOwnerCount =
        TenantTerminationProcess.MaximumFrozenOwners;

    private TenantTerminationTerminalReceipt() { }

    private TenantTerminationTerminalReceipt(Guid id, string scopeId)
        : base(id, scopeId) { }

    public Guid IdempotencyKey { get; private set; }
    public Guid ProcessId { get; private set; }
    public Guid CaseId { get; private set; }
    public long ApprovalRevision { get; private set; }
    public Guid TerminationEpoch { get; private set; }
    public long DestroyOperationRevision { get; private set; }
    public long VerificationOperationRevision { get; private set; }
    public Guid VerificationTaskRunId { get; private set; }
    public int VerificationTaskAttempt { get; private set; }
    public string PolicyEvidenceSha256 { get; private set; } = string.Empty;
    public string FrozenRevisionSha256 { get; private set; } = string.Empty;
    public bool ExportRequested { get; private set; }
    public Guid? ExportArtifactId { get; private set; }
    public long? ExportArtifactVersion { get; private set; }
    public string? ExportFragmentSetSha256 { get; private set; }
    public int OwnerCount { get; private set; }
    public string OwnerProofSetSha256 { get; private set; } = string.Empty;
    public string TerminalOwnerKey { get; private set; } = string.Empty;
    public long TerminalOwnerSelectedProofRevision { get; private set; }
    public long TerminalOwnerResultingProofRevision { get; private set; }
    public long ReplayCheckpointSequence { get; private set; }
    public string ReplayCheckpointRecordSha256 { get; private set; } =
        string.Empty;
    public int ReplayIntegrityKeyVersion { get; private set; }
    public string ReplayCheckpointProofSha256 { get; private set; } =
        string.Empty;
    public DateTimeOffset ReplayCheckpointFlushedAtUtc { get; private set; }
    public string SealedBy { get; private set; } = string.Empty;
    public DateTimeOffset SealedAtUtc { get; private set; }
    public long Version { get; private set; } = 1;

    public static Result<TenantTerminationTerminalReceipt> Seal(
        Guid receiptId,
        string tenantId,
        Guid idempotencyKey,
        Guid processId,
        Guid caseId,
        long approvalRevision,
        Guid terminationEpoch,
        long destroyOperationRevision,
        long verificationOperationRevision,
        Guid verificationTaskRunId,
        int verificationTaskAttempt,
        string policyEvidenceSha256,
        string frozenRevisionSha256,
        bool exportRequested,
        Guid? exportArtifactId,
        long? exportArtifactVersion,
        string? exportFragmentSetSha256,
        int ownerCount,
        string ownerProofSetSha256,
        string terminalOwnerKey,
        long terminalOwnerSelectedProofRevision,
        long terminalOwnerResultingProofRevision,
        long replayCheckpointSequence,
        string replayCheckpointRecordSha256,
        int replayIntegrityKeyVersion,
        string replayCheckpointProofSha256,
        DateTimeOffset replayCheckpointFlushedAtUtc,
        string sealedBy,
        DateTimeOffset sealedAtUtc)
    {
        string normalizedOwnerKey = terminalOwnerKey?.Trim() ?? string.Empty;
        string normalizedActor = sealedBy?.Trim() ?? string.Empty;
        bool exportShapeValid = exportRequested
            ? exportArtifactId is Guid artifactId &&
                artifactId != Guid.Empty &&
                exportArtifactVersion is > 0 &&
                TenantTerminationProcess.IsSha256(exportFragmentSetSha256)
            : !exportArtifactId.HasValue &&
                !exportArtifactVersion.HasValue &&
                exportFragmentSetSha256 is null;
        if (receiptId == Guid.Empty ||
            idempotencyKey == Guid.Empty ||
            processId == Guid.Empty ||
            caseId == Guid.Empty ||
            approvalRevision <= 0 ||
            terminationEpoch == Guid.Empty ||
            destroyOperationRevision <= approvalRevision ||
            verificationOperationRevision <= destroyOperationRevision ||
            verificationTaskRunId == Guid.Empty ||
            verificationTaskAttempt <= 0 ||
            !TenantTerminationProcess.IsSha256(policyEvidenceSha256) ||
            !TenantTerminationProcess.IsSha256(frozenRevisionSha256) ||
            !exportShapeValid ||
            ownerCount is <= 0 or > MaximumOwnerCount ||
            !TenantTerminationProcess.IsSha256(ownerProofSetSha256) ||
            !TenantTerminationProcess.IsStableCode(
                normalizedOwnerKey,
                OwnerKeyMaxLength) ||
            terminalOwnerSelectedProofRevision < 0 ||
            terminalOwnerResultingProofRevision <
                terminalOwnerSelectedProofRevision ||
            replayCheckpointSequence <= 0 ||
            !TenantTerminationProcess.IsSha256(
                replayCheckpointRecordSha256) ||
            replayIntegrityKeyVersion <= 0 ||
            !TenantTerminationProcess.IsSha256(
                replayCheckpointProofSha256) ||
            replayCheckpointFlushedAtUtc == default ||
            normalizedActor.Length is <= 0 or >
                TenantTerminationProcess.ActorIdMaxLength ||
            sealedAtUtc < replayCheckpointFlushedAtUtc)
        {
            return Invalid();
        }

        if (!TenantIds.TryNormalize(tenantId, out string? scopeId))
        {
            return Result.Failure<TenantTerminationTerminalReceipt>(
                DataRightsDomainErrors.TenantInvalid);
        }

        return Result.Success(new TenantTerminationTerminalReceipt(
            receiptId,
            scopeId)
        {
            IdempotencyKey = idempotencyKey,
            ProcessId = processId,
            CaseId = caseId,
            ApprovalRevision = approvalRevision,
            TerminationEpoch = terminationEpoch,
            DestroyOperationRevision = destroyOperationRevision,
            VerificationOperationRevision = verificationOperationRevision,
            VerificationTaskRunId = verificationTaskRunId,
            VerificationTaskAttempt = verificationTaskAttempt,
            PolicyEvidenceSha256 = policyEvidenceSha256,
            FrozenRevisionSha256 = frozenRevisionSha256,
            ExportRequested = exportRequested,
            ExportArtifactId = exportArtifactId,
            ExportArtifactVersion = exportArtifactVersion,
            ExportFragmentSetSha256 = exportFragmentSetSha256,
            OwnerCount = ownerCount,
            OwnerProofSetSha256 = ownerProofSetSha256,
            TerminalOwnerKey = normalizedOwnerKey,
            TerminalOwnerSelectedProofRevision =
                terminalOwnerSelectedProofRevision,
            TerminalOwnerResultingProofRevision =
                terminalOwnerResultingProofRevision,
            ReplayCheckpointSequence = replayCheckpointSequence,
            ReplayCheckpointRecordSha256 = replayCheckpointRecordSha256,
            ReplayIntegrityKeyVersion = replayIntegrityKeyVersion,
            ReplayCheckpointProofSha256 = replayCheckpointProofSha256,
            ReplayCheckpointFlushedAtUtc = replayCheckpointFlushedAtUtc,
            SealedBy = normalizedActor,
            SealedAtUtc = sealedAtUtc
        });
    }

    private static Result<TenantTerminationTerminalReceipt> Invalid() =>
        Result.Failure<TenantTerminationTerminalReceipt>(
            DataRightsDomainErrors.TenantTerminationTerminalReceiptInvalid);
}

namespace BunkFy.Modules.Ingestion.Domain.DataRights;

using BunkFy.Modules.Ingestion.Domain.Errors;
using Gma.Framework.Domain.Models;
using Gma.Framework.Naming;
using Gma.Framework.Results;

public sealed partial class IngestionAnonymisationTombstone
    : ScopedAggregateRoot<Guid>
{
    public const int CurrentContractVersion = 2;
    public const int Sha256Length = 64;

    private IngestionAnonymisationTombstone() { }

    private IngestionAnonymisationTombstone(
        Guid id,
        string scopeId)
        : base(id, scopeId)
    {
    }

    public int ContractVersion { get; private set; }
    public long Revision { get; private set; }
    public IngestionAnonymisationTombstoneState State { get; private set; }
    public IngestionAnonymisationOrigin Origin { get; private set; }
    public Guid PropertyId { get; private set; }
    public Guid ConnectionId { get; private set; }
    public long SelectedSourceLinkVersion { get; private set; }
    public long ResultingSourceLinkVersion { get; private set; }
    public Guid WorkItemId { get; private set; }
    public Guid IdempotencyKey { get; private set; }
    public Guid CaseId { get; private set; }
    public long ApprovalRevision { get; private set; }
    public long OperationRevision { get; private set; }
    public string ApprovalEvidenceSha256 { get; private set; } =
        string.Empty;
    public string PolicyEvidenceSha256 { get; private set; } =
        string.Empty;
    public string OperationFenceSha256 { get; private set; } =
        string.Empty;
    public string ActorId { get; private set; } = string.Empty;
    public DateTimeOffset ExecutionStartedAtUtc { get; private set; }
    public int OwnerReceiptContractVersion { get; private set; }
    public Guid OwnerReceiptId { get; private set; }
    public string OwnerReceiptSha256 { get; private set; } =
        string.Empty;
    public DateTimeOffset OriginallyCompletedAtUtc { get; private set; }
    public Guid LedgerEntryId { get; private set; }
    public long TenantSequence { get; private set; }
    public string LedgerEntrySha256 { get; private set; } =
        string.Empty;
    public int GraphRecordCount { get; private set; }
    public int FingerprintCount { get; private set; }
    public int RawPayloadCount { get; private set; }
    public DateTimeOffset ReplayStartedAtUtc { get; private set; }
    public DateTimeOffset? LastReplayedAtUtc { get; private set; }

    public Guid ReductionClaimId =>
        this.Origin == IngestionAnonymisationOrigin.LiveExecution
            ? this.OwnerReceiptId
            : this.LedgerEntryId;

    public bool IsProtectedReplayPending =>
        this.LedgerEntryId != Guid.Empty &&
        !this.LastReplayedAtUtc.HasValue;

    public static Result<IngestionAnonymisationTombstone>
        BeginExecution(
            string tenantId,
            Guid sourceLinkId,
            Guid propertyId,
            Guid connectionId,
            long selectedSourceLinkVersion,
            Guid workItemId,
            Guid idempotencyKey,
            Guid caseId,
            long approvalRevision,
            long operationRevision,
            Guid ownerReceiptId,
            string approvalEvidenceSha256,
            string policyEvidenceSha256,
            string operationFenceSha256,
            string actorId,
            int graphRecordCount,
            int fingerprintCount,
            int rawPayloadCount,
            DateTimeOffset executionStartedAtUtc)
    {
        string approvalDigest = NormalizeSha256(
            approvalEvidenceSha256);
        string policyDigest = NormalizeSha256(policyEvidenceSha256);
        string operationDigest = NormalizeSha256(
            operationFenceSha256);
        string actor = actorId?.Trim() ?? string.Empty;
        if (!TenantIds.TryNormalize(
                tenantId,
                out string? scopeId) ||
            sourceLinkId == Guid.Empty ||
            propertyId == Guid.Empty ||
            connectionId == Guid.Empty ||
            selectedSourceLinkVersion <= 0 ||
            workItemId == Guid.Empty ||
            idempotencyKey == Guid.Empty ||
            caseId == Guid.Empty ||
            approvalRevision <= 0 ||
            operationRevision <= approvalRevision ||
            ownerReceiptId == Guid.Empty ||
            !IsSha256(approvalDigest) ||
            !IsSha256(policyDigest) ||
            !IsSha256(operationDigest) ||
            actor.Length is 0 or >
                IngestionAnonymisationReceipt.ActorIdMaxLength ||
            graphRecordCount <= 0 ||
            fingerprintCount <= 0 ||
            rawPayloadCount < 0 ||
            executionStartedAtUtc == default)
        {
            return Invalid();
        }

        return Result.Success(
            new IngestionAnonymisationTombstone(
                sourceLinkId,
                scopeId)
            {
                ContractVersion = CurrentContractVersion,
                Revision = 1,
                State =
                    IngestionAnonymisationTombstoneState.Reducing,
                Origin =
                    IngestionAnonymisationOrigin.LiveExecution,
                PropertyId = propertyId,
                ConnectionId = connectionId,
                SelectedSourceLinkVersion =
                    selectedSourceLinkVersion,
                ResultingSourceLinkVersion =
                    checked(selectedSourceLinkVersion + 1),
                WorkItemId = workItemId,
                IdempotencyKey = idempotencyKey,
                CaseId = caseId,
                ApprovalRevision = approvalRevision,
                OperationRevision = operationRevision,
                ApprovalEvidenceSha256 = approvalDigest,
                PolicyEvidenceSha256 = policyDigest,
                OperationFenceSha256 = operationDigest,
                ActorId = actor,
                ExecutionStartedAtUtc =
                    executionStartedAtUtc.ToUniversalTime(),
                OwnerReceiptContractVersion =
                    IngestionAnonymisationReceipt
                        .CurrentContractVersion,
                OwnerReceiptId = ownerReceiptId,
                GraphRecordCount = graphRecordCount,
                FingerprintCount = fingerprintCount,
                RawPayloadCount = rawPayloadCount
            });
    }

    public static Result<IngestionAnonymisationTombstone>
        BeginRestore(
            string tenantId,
            Guid sourceLinkId,
            Guid propertyId,
            Guid connectionId,
            long selectedSourceLinkVersion,
            long resultingSourceLinkVersion,
            int ownerReceiptContractVersion,
            Guid ownerReceiptId,
            string ownerReceiptSha256,
            DateTimeOffset originallyCompletedAtUtc,
            Guid ledgerEntryId,
            long tenantSequence,
            string ledgerEntrySha256,
            int graphRecordCount,
            int fingerprintCount,
            int rawPayloadCount,
            DateTimeOffset replayStartedAtUtc)
    {
        string receiptDigest = NormalizeSha256(
            ownerReceiptSha256);
        string ledgerDigest = NormalizeSha256(
            ledgerEntrySha256);
        if (!TenantIds.TryNormalize(
                tenantId,
                out string? scopeId) ||
            sourceLinkId == Guid.Empty ||
            propertyId == Guid.Empty ||
            connectionId == Guid.Empty ||
            selectedSourceLinkVersion <= 0 ||
            resultingSourceLinkVersion !=
                selectedSourceLinkVersion + 1 ||
            ownerReceiptContractVersion <= 0 ||
            ownerReceiptId == Guid.Empty ||
            !IsSha256(receiptDigest) ||
            originallyCompletedAtUtc == default ||
            ledgerEntryId == Guid.Empty ||
            tenantSequence <= 0 ||
            !IsSha256(ledgerDigest) ||
            graphRecordCount <= 0 ||
            fingerprintCount <= 0 ||
            rawPayloadCount < 0 ||
            replayStartedAtUtc == default)
        {
            return Invalid();
        }

        return Result.Success(
            new IngestionAnonymisationTombstone(
                sourceLinkId,
                scopeId)
            {
                ContractVersion = CurrentContractVersion,
                Revision = 1,
                State =
                    IngestionAnonymisationTombstoneState.Reducing,
                Origin =
                    IngestionAnonymisationOrigin.ProtectedLedgerReplay,
                PropertyId = propertyId,
                ConnectionId = connectionId,
                SelectedSourceLinkVersion =
                    selectedSourceLinkVersion,
                ResultingSourceLinkVersion =
                    resultingSourceLinkVersion,
                OwnerReceiptContractVersion =
                    ownerReceiptContractVersion,
                OwnerReceiptId = ownerReceiptId,
                OwnerReceiptSha256 = receiptDigest,
                OriginallyCompletedAtUtc =
                    originallyCompletedAtUtc.ToUniversalTime(),
                LedgerEntryId = ledgerEntryId,
                TenantSequence = tenantSequence,
                LedgerEntrySha256 = ledgerDigest,
                GraphRecordCount = graphRecordCount,
                FingerprintCount = fingerprintCount,
                RawPayloadCount = rawPayloadCount,
                ReplayStartedAtUtc =
                    replayStartedAtUtc.ToUniversalTime()
            });
    }

    public Result CompleteExecution(
        IngestionAnonymisationReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        if (this.Origin !=
                IngestionAnonymisationOrigin.LiveExecution ||
            this.State !=
                IngestionAnonymisationTombstoneState.Reducing ||
            !receipt.MatchesExecution(
                this.WorkItemId,
                this.IdempotencyKey,
                this.PropertyId,
                this.CaseId,
                this.ApprovalRevision,
                this.OperationRevision,
                this.Id,
                this.SelectedSourceLinkVersion,
                this.ApprovalEvidenceSha256,
                this.ActorId) ||
            receipt.Id != this.OwnerReceiptId ||
            receipt.ResultingSourceLinkVersion !=
                this.ResultingSourceLinkVersion ||
            receipt.GraphRecordCount != this.GraphRecordCount ||
            receipt.FingerprintCount != this.FingerprintCount ||
            receipt.RawPayloadCount != this.RawPayloadCount ||
            !string.Equals(
                receipt.PolicyEvidenceSha256,
                this.PolicyEvidenceSha256,
                StringComparison.Ordinal) ||
            !string.Equals(
                receipt.OperationFenceSha256,
                this.OperationFenceSha256,
                StringComparison.Ordinal))
        {
            return InvalidResult();
        }

        this.OwnerReceiptSha256 = receipt.CanonicalSha256;
        this.OriginallyCompletedAtUtc = receipt.CompletedAtUtc;
        this.State =
            IngestionAnonymisationTombstoneState.Completed;
        this.Revision = checked(this.Revision + 1);
        return Result.Success();
    }

    public Result BeginProtectedReplay(
        Guid propertyId,
        int ownerReceiptContractVersion,
        Guid ownerReceiptId,
        string ownerReceiptSha256,
        long resultingSourceLinkVersion,
        DateTimeOffset originallyCompletedAtUtc,
        Guid ledgerEntryId,
        long tenantSequence,
        string ledgerEntrySha256,
        DateTimeOffset replayStartedAtUtc)
    {
        string ledgerDigest = NormalizeSha256(
            ledgerEntrySha256);
        DateTimeOffset started =
            replayStartedAtUtc.ToUniversalTime();
        if (this.State !=
                IngestionAnonymisationTombstoneState.Completed ||
            !this.MatchesOwnerProof(
                propertyId,
                ownerReceiptContractVersion,
                ownerReceiptId,
                ownerReceiptSha256,
                resultingSourceLinkVersion,
                originallyCompletedAtUtc) ||
            ledgerEntryId == Guid.Empty ||
            tenantSequence <= 0 ||
            !IsSha256(ledgerDigest) ||
            started == default)
        {
            return InvalidResult();
        }

        if (this.LedgerEntryId != Guid.Empty)
        {
            return this.MatchesProtectedLedger(
                ledgerEntryId,
                tenantSequence,
                ledgerDigest)
                ? Result.Success()
                : InvalidResult();
        }

        this.LedgerEntryId = ledgerEntryId;
        this.TenantSequence = tenantSequence;
        this.LedgerEntrySha256 = ledgerDigest;
        this.ReplayStartedAtUtc = started;
        this.Revision = checked(this.Revision + 1);
        return Result.Success();
    }

    public Result CompleteRestore(DateTimeOffset replayedAtUtc)
    {
        DateTimeOffset completed =
            replayedAtUtc.ToUniversalTime();
        if (completed == default ||
            this.LedgerEntryId == Guid.Empty ||
            this.TenantSequence <= 0 ||
            !IsSha256(this.LedgerEntrySha256) ||
            completed < this.ReplayStartedAtUtc)
        {
            return InvalidResult();
        }

        if (this.LastReplayedAtUtc.HasValue)
        {
            return this.LastReplayedAtUtc.Value == completed
                ? Result.Success()
                : InvalidResult();
        }

        if (this.State ==
            IngestionAnonymisationTombstoneState.Reducing)
        {
            if (this.Origin !=
                IngestionAnonymisationOrigin
                    .ProtectedLedgerReplay)
            {
                return InvalidResult();
            }

            this.State =
                IngestionAnonymisationTombstoneState.Completed;
        }
        else if (this.State !=
                 IngestionAnonymisationTombstoneState.Completed)
        {
            return InvalidResult();
        }

        this.LastReplayedAtUtc = completed;
        this.Revision = checked(this.Revision + 1);
        return Result.Success();
    }

    private static string NormalizeSha256(string? value) =>
        value?.Trim().ToLowerInvariant() ?? string.Empty;

    private static bool IsSha256(string value) =>
        value.Length == Sha256Length &&
        value.All(character =>
            character is (>= '0' and <= '9') or
                (>= 'a' and <= 'f'));

    private static Result<IngestionAnonymisationTombstone>
        Invalid() =>
        Result.Failure<IngestionAnonymisationTombstone>(
            IngestionDomainErrors.AnonymisationTombstoneInvalid);

    private static Result InvalidResult() =>
        Result.Failure(
            IngestionDomainErrors.AnonymisationTombstoneInvalid);
}

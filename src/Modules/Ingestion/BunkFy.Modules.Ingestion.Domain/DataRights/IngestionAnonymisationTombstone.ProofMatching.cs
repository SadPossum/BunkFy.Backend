namespace BunkFy.Modules.Ingestion.Domain.DataRights;

public sealed partial class IngestionAnonymisationTombstone
{
    public bool MatchesExecution(
        Guid workItemId,
        Guid idempotencyKey,
        Guid propertyId,
        Guid caseId,
        long approvalRevision,
        long operationRevision,
        long selectedSourceLinkVersion,
        string approvalEvidenceSha256,
        string actorId) =>
        this.ContractVersion == CurrentContractVersion &&
        this.Origin ==
            IngestionAnonymisationOrigin.LiveExecution &&
        this.WorkItemId == workItemId &&
        this.IdempotencyKey == idempotencyKey &&
        this.PropertyId == propertyId &&
        this.CaseId == caseId &&
        this.ApprovalRevision == approvalRevision &&
        this.OperationRevision == operationRevision &&
        this.SelectedSourceLinkVersion ==
            selectedSourceLinkVersion &&
        string.Equals(
            this.ApprovalEvidenceSha256,
            NormalizeSha256(approvalEvidenceSha256),
            StringComparison.Ordinal) &&
        string.Equals(
            this.ActorId,
            actorId?.Trim(),
            StringComparison.Ordinal);

    public bool MatchesOwnerProof(
        Guid propertyId,
        int ownerReceiptContractVersion,
        Guid ownerReceiptId,
        string ownerReceiptSha256,
        long resultingSourceLinkVersion,
        DateTimeOffset originallyCompletedAtUtc) =>
        this.ContractVersion == CurrentContractVersion &&
        this.State ==
            IngestionAnonymisationTombstoneState.Completed &&
        this.MatchesStoredOwnerProof(
            propertyId,
            ownerReceiptContractVersion,
            ownerReceiptId,
            ownerReceiptSha256,
            resultingSourceLinkVersion,
            originallyCompletedAtUtc);

    public bool MatchesProtectedLedger(
        Guid ledgerEntryId,
        long tenantSequence,
        string ledgerEntrySha256) =>
        this.LedgerEntryId == ledgerEntryId &&
        this.TenantSequence == tenantSequence &&
        string.Equals(
            this.LedgerEntrySha256,
            NormalizeSha256(ledgerEntrySha256),
            StringComparison.Ordinal);

    public bool MatchesRestore(
        Guid propertyId,
        int ownerReceiptContractVersion,
        Guid ownerReceiptId,
        string ownerReceiptSha256,
        DateTimeOffset originallyCompletedAtUtc,
        Guid ledgerEntryId,
        long tenantSequence,
        string ledgerEntrySha256) =>
        this.ContractVersion == CurrentContractVersion &&
        this.MatchesStoredOwnerProof(
            propertyId,
            ownerReceiptContractVersion,
            ownerReceiptId,
            ownerReceiptSha256,
            this.ResultingSourceLinkVersion,
            originallyCompletedAtUtc) &&
        this.MatchesProtectedLedger(
            ledgerEntryId,
            tenantSequence,
            ledgerEntrySha256);

    private bool MatchesStoredOwnerProof(
        Guid propertyId,
        int ownerReceiptContractVersion,
        Guid ownerReceiptId,
        string ownerReceiptSha256,
        long resultingSourceLinkVersion,
        DateTimeOffset originallyCompletedAtUtc) =>
        this.PropertyId == propertyId &&
        this.OwnerReceiptContractVersion ==
            ownerReceiptContractVersion &&
        this.OwnerReceiptId == ownerReceiptId &&
        string.Equals(
            this.OwnerReceiptSha256,
            NormalizeSha256(ownerReceiptSha256),
            StringComparison.Ordinal) &&
        this.ResultingSourceLinkVersion ==
            resultingSourceLinkVersion &&
        this.OriginallyCompletedAtUtc ==
            originallyCompletedAtUtc.ToUniversalTime();
}

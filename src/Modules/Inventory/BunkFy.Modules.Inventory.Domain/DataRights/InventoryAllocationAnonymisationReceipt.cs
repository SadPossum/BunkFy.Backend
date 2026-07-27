namespace BunkFy.Modules.Inventory.Domain.DataRights;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using BunkFy.Modules.Inventory.Domain.Aggregates;
using BunkFy.Modules.Inventory.Domain.Errors;
using Gma.Framework.Domain.Models;
using Gma.Framework.Naming;
using Gma.Framework.Results;

public sealed class InventoryAllocationAnonymisationReceipt
    : ScopedAggregateRoot<Guid>
{
    public const int ActorIdMaxLength = 200;
    public const int CurrentContractVersion = 1;
    public const int Sha256Length = 64;

    private InventoryAllocationAnonymisationReceipt() { }

    private InventoryAllocationAnonymisationReceipt(
        Guid id,
        string scopeId)
        : base(id, scopeId)
    {
    }

    public int ContractVersion { get; private set; }
    public Guid WorkItemId { get; private set; }
    public Guid IdempotencyKey { get; private set; }
    public Guid PropertyId { get; private set; }
    public Guid CaseId { get; private set; }
    public long ApprovalRevision { get; private set; }
    public long OperationRevision { get; private set; }
    public Guid AllocationId { get; private set; }
    public long SelectedAllocationVersion { get; private set; }
    public long ResultingAllocationVersion { get; private set; }
    public Guid ResultingReservationPseudonym { get; private set; }
    public InventoryAllocationAnonymisationDisposition Disposition
    {
        get;
        private set;
    }
    public InventoryAllocationAnonymisationReason Reason
    {
        get;
        private set;
    }
    public int RemovedAmendmentDecisionCount { get; private set; }
    public string ApprovalEvidenceSha256 { get; private set; } =
        string.Empty;
    public string ActorId { get; private set; } = string.Empty;
    public DateTimeOffset CompletedAtUtc { get; private set; }
    public string CanonicalSha256 { get; private set; } = string.Empty;

    public static Result<InventoryAllocationAnonymisationReceipt> Create(
        Guid id,
        string tenantId,
        Guid workItemId,
        Guid idempotencyKey,
        Guid propertyId,
        Guid caseId,
        long approvalRevision,
        long operationRevision,
        Guid allocationId,
        InventoryAllocationAnonymisationOutcome outcome,
        int removedAmendmentDecisionCount,
        string approvalEvidenceSha256,
        string actorId)
    {
        string actor = actorId?.Trim() ?? string.Empty;
        string approvalHash =
            approvalEvidenceSha256?.Trim().ToLowerInvariant() ??
            string.Empty;
        if (id == Guid.Empty ||
            workItemId == Guid.Empty ||
            idempotencyKey == Guid.Empty ||
            propertyId == Guid.Empty ||
            caseId == Guid.Empty ||
            allocationId == Guid.Empty ||
            !TenantIds.TryNormalize(tenantId, out string? scopeId) ||
            approvalRevision <= 0 ||
            operationRevision <= approvalRevision ||
            outcome.SelectedVersion <= 0 ||
            outcome.ResultingVersion != outcome.SelectedVersion + 1 ||
            outcome.ResultingReservationPseudonym == Guid.Empty ||
            outcome.CompletedAtUtc == default ||
            outcome.CompletedAtUtc.Offset != TimeSpan.Zero ||
            removedAmendmentDecisionCount < 0 ||
            actor.Length is 0 or > ActorIdMaxLength ||
            !IsSha256(approvalHash))
        {
            return Invalid();
        }

        InventoryAllocationAnonymisationReceipt receipt =
            new(id, scopeId)
            {
                ContractVersion = CurrentContractVersion,
                WorkItemId = workItemId,
                IdempotencyKey = idempotencyKey,
                PropertyId = propertyId,
                CaseId = caseId,
                ApprovalRevision = approvalRevision,
                OperationRevision = operationRevision,
                AllocationId = allocationId,
                SelectedAllocationVersion = outcome.SelectedVersion,
                ResultingAllocationVersion = outcome.ResultingVersion,
                ResultingReservationPseudonym =
                    outcome.ResultingReservationPseudonym,
                Disposition =
                    InventoryAllocationAnonymisationDisposition.Completed,
                Reason = InventoryAllocationAnonymisationReason
                    .ReservationCorrelationPseudonymised,
                RemovedAmendmentDecisionCount =
                    removedAmendmentDecisionCount,
                ApprovalEvidenceSha256 = approvalHash,
                ActorId = actor,
                CompletedAtUtc = outcome.CompletedAtUtc
            };
        receipt.CanonicalSha256 = receipt.ComputeCanonicalSha256();
        return Result.Success(receipt);
    }

    public bool MatchesExecution(
        Guid workItemId,
        Guid idempotencyKey,
        Guid propertyId,
        Guid caseId,
        long approvalRevision,
        long operationRevision,
        Guid allocationId,
        long selectedAllocationVersion,
        string approvalEvidenceSha256,
        string actorId) =>
        this.ContractVersion == CurrentContractVersion &&
        this.WorkItemId == workItemId &&
        this.IdempotencyKey == idempotencyKey &&
        this.PropertyId == propertyId &&
        this.CaseId == caseId &&
        this.ApprovalRevision == approvalRevision &&
        this.OperationRevision == operationRevision &&
        this.AllocationId == allocationId &&
        this.SelectedAllocationVersion == selectedAllocationVersion &&
        string.Equals(
            this.ApprovalEvidenceSha256,
            approvalEvidenceSha256?.Trim(),
            StringComparison.OrdinalIgnoreCase) &&
        string.Equals(
            this.ActorId,
            actorId?.Trim(),
            StringComparison.Ordinal);

    public bool MatchesOwnerProof(
        int contractVersion,
        Guid receiptId,
        Guid propertyId,
        Guid allocationId,
        long resultingAllocationVersion,
        string canonicalSha256,
        DateTimeOffset completedAtUtc) =>
        this.ContractVersion == CurrentContractVersion &&
        contractVersion == CurrentContractVersion &&
        this.Id == receiptId &&
        this.PropertyId == propertyId &&
        this.AllocationId == allocationId &&
        this.ResultingAllocationVersion == resultingAllocationVersion &&
        string.Equals(
            this.CanonicalSha256,
            canonicalSha256,
            StringComparison.Ordinal) &&
        this.CompletedAtUtc == completedAtUtc.ToUniversalTime() &&
        string.Equals(
            this.CanonicalSha256,
            NormalizeSha256(canonicalSha256),
            StringComparison.Ordinal) &&
        string.Equals(
            this.CanonicalSha256,
            this.ComputeCanonicalSha256(),
            StringComparison.Ordinal);

    private string ComputeCanonicalSha256()
    {
        StringBuilder canonical = new();
        Append(canonical, Invariant(this.ContractVersion));
        Append(canonical, this.ScopeId);
        Append(canonical, this.Id.ToString("N"));
        Append(canonical, this.WorkItemId.ToString("N"));
        Append(canonical, this.IdempotencyKey.ToString("N"));
        Append(canonical, this.PropertyId.ToString("N"));
        Append(canonical, this.CaseId.ToString("N"));
        Append(canonical, Invariant(this.ApprovalRevision));
        Append(canonical, Invariant(this.OperationRevision));
        Append(canonical, this.AllocationId.ToString("N"));
        Append(canonical, Invariant(this.SelectedAllocationVersion));
        Append(canonical, Invariant(this.ResultingAllocationVersion));
        Append(
            canonical,
            this.ResultingReservationPseudonym.ToString("N"));
        Append(canonical, Invariant((int)this.Disposition));
        Append(canonical, Invariant((int)this.Reason));
        Append(
            canonical,
            Invariant(this.RemovedAmendmentDecisionCount));
        Append(canonical, this.ApprovalEvidenceSha256);
        Append(canonical, this.ActorId);
        Append(
            canonical,
            this.CompletedAtUtc.ToString("O", CultureInfo.InvariantCulture));
        return Convert.ToHexStringLower(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(canonical.ToString())));
    }

    private static void Append(StringBuilder target, string value)
    {
        target.Append(
            value.Length.ToString(CultureInfo.InvariantCulture));
        target.Append(':');
        target.Append(value);
    }

    private static string Invariant(long value) =>
        value.ToString(CultureInfo.InvariantCulture);

    private static string NormalizeSha256(string? value) =>
        value?.Trim().ToLowerInvariant() ?? string.Empty;

    private static bool IsSha256(string value) =>
        value.Length == Sha256Length &&
        value.All(character =>
            character is (>= '0' and <= '9') or
                (>= 'a' and <= 'f'));

    private static Result<InventoryAllocationAnonymisationReceipt>
        Invalid() =>
        Result.Failure<InventoryAllocationAnonymisationReceipt>(
            InventoryDomainErrors.AllocationAnonymisationReceiptInvalid);
}

public enum InventoryAllocationAnonymisationDisposition
{
    Unknown = 0,
    Completed = 1
}

public enum InventoryAllocationAnonymisationReason
{
    Unknown = 0,
    ReservationCorrelationPseudonymised = 1
}

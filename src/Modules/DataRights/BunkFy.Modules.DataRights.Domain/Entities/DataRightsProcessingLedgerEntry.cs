namespace BunkFy.Modules.DataRights.Domain.Entities;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Errors;
using BunkFy.Modules.DataRights.Domain.Models;
using BunkFy.Modules.DataRights.Domain.ValueObjects;
using Gma.Framework.Domain.Models;
using Gma.Framework.Results;

public sealed class DataRightsProcessingLedgerEntry : ScopedEntity<Guid>
{
    public const int CurrentContractVersion = 1;
    public const int CodeMaxLength = DataRightsExecutionWorkItem.OwnerCodeMaxLength;
    public const int Sha256Length = DataRightsRecordPseudonym.Sha256Length;
    public const string GenesisEntrySha256 =
        "0000000000000000000000000000000000000000000000000000000000000000";

    private DataRightsProcessingLedgerEntry() { }

    private DataRightsProcessingLedgerEntry(Guid id, string scopeId)
        : base(id, scopeId)
    {
    }

    public int ContractVersion { get; private set; }
    public long TenantSequence { get; private set; }
    public Guid WorkItemId { get; private set; }
    public Guid CaseId { get; private set; }
    public long ApprovalRevision { get; private set; }
    public long OperationRevision { get; private set; }
    public DataRightsCaseOperation Operation { get; private set; }
    public Guid RoutingPropertyId { get; private set; }
    public string OwnerKey { get; private set; } = string.Empty;
    public string RecordType { get; private set; } = string.Empty;
    public int RecordPseudonymKeyVersion { get; private set; }
    public string RecordPseudonymSha256 { get; private set; } = string.Empty;
    public string DispositionCode { get; private set; } = string.Empty;
    public string ReasonCode { get; private set; } = string.Empty;
    public DateTimeOffset CompletedAtUtc { get; private set; }
    public int PolicyEvidenceSchemaVersion { get; private set; }
    public string PolicyId { get; private set; } = string.Empty;
    public int PolicyVersion { get; private set; }
    public string PolicyContentSha256 { get; private set; } = string.Empty;
    public string RetentionPolicyId { get; private set; } = string.Empty;
    public int RetentionPolicyVersion { get; private set; }
    public int OwnerReceiptContractVersion { get; private set; }
    public Guid OwnerReceiptId { get; private set; }
    public string OwnerReceiptSha256 { get; private set; } = string.Empty;
    public string PreviousEntrySha256 { get; private set; } = string.Empty;
    public string EntrySha256 { get; private set; } = string.Empty;
    public Guid? ReplayOfLedgerEntryId { get; private set; }
    public Guid? SupersedesLedgerEntryId { get; private set; }

    public static Result<DataRightsProcessingLedgerEntry> Create(
        Guid entryId,
        long tenantSequence,
        DataRightsExecutionWorkItem workItem,
        DataRightsRecordPseudonym recordPseudonym,
        string previousEntrySha256,
        Guid? replayOfLedgerEntryId = null,
        Guid? supersedesLedgerEntryId = null)
    {
        ArgumentNullException.ThrowIfNull(workItem);
        ArgumentNullException.ThrowIfNull(recordPseudonym);

        string previousDigest = NormalizeSha256(previousEntrySha256);
        string ownerReceiptDigest = NormalizeSha256(workItem.OwnerReceiptSha256);
        bool hasOwnerProof =
            workItem.State == DataRightsExecutionWorkItemState.OwnerProofRecorded &&
            workItem.OwnerReceiptContractVersion is > 0 &&
            workItem.OwnerReceiptId.HasValue &&
            workItem.OwnerCompletedAtUtc.HasValue &&
            !string.IsNullOrWhiteSpace(workItem.OwnerDispositionCode) &&
            !string.IsNullOrWhiteSpace(workItem.OwnerReasonCode) &&
            IsSha256(ownerReceiptDigest);
        bool usesGenesisDigest = string.Equals(
            previousDigest,
            GenesisEntrySha256,
            StringComparison.Ordinal);
        bool duplicatesReplayCoordinate =
            replayOfLedgerEntryId.HasValue &&
            replayOfLedgerEntryId == supersedesLedgerEntryId;
        bool sequenceConflict = tenantSequence == 1
            ? !usesGenesisDigest
            : usesGenesisDigest;

        if (entryId == Guid.Empty ||
            tenantSequence <= 0 ||
            workItem.Id == Guid.Empty ||
            workItem.CaseId == Guid.Empty ||
            workItem.PropertyId == Guid.Empty ||
            workItem.ApprovalRevision <= 0 ||
            workItem.ExecutionRevision <= workItem.ApprovalRevision ||
            workItem.Operation != DataRightsCaseOperation.Anonymisation ||
            !hasOwnerProof ||
            !IsSha256(previousDigest) ||
            sequenceConflict ||
            replayOfLedgerEntryId == entryId ||
            supersedesLedgerEntryId == entryId ||
            duplicatesReplayCoordinate)
        {
            return Invalid();
        }

        string disposition = workItem.OwnerDispositionCode!.Trim();
        string reason = workItem.OwnerReasonCode!.Trim();
        if (disposition.Length is 0 or > CodeMaxLength ||
            reason.Length is 0 or > CodeMaxLength)
        {
            return Invalid();
        }

        DataRightsProcessingLedgerEntry entry = new(entryId, workItem.ScopeId)
        {
            ContractVersion = CurrentContractVersion,
            TenantSequence = tenantSequence,
            WorkItemId = workItem.Id,
            CaseId = workItem.CaseId,
            ApprovalRevision = workItem.ApprovalRevision,
            OperationRevision = workItem.ExecutionRevision,
            Operation = workItem.Operation,
            RoutingPropertyId = workItem.PropertyId,
            OwnerKey = workItem.OwnerKey,
            RecordType = workItem.RecordType,
            RecordPseudonymKeyVersion = recordPseudonym.KeyVersion,
            RecordPseudonymSha256 = recordPseudonym.Sha256,
            DispositionCode = disposition,
            ReasonCode = reason,
            CompletedAtUtc = workItem.OwnerCompletedAtUtc!.Value.ToUniversalTime(),
            PolicyEvidenceSchemaVersion = workItem.PolicyEvidenceSchemaVersion,
            PolicyId = workItem.PolicyId,
            PolicyVersion = workItem.PolicyVersion,
            PolicyContentSha256 = NormalizeSha256(workItem.PolicyContentSha256),
            RetentionPolicyId = workItem.RetentionPolicyId,
            RetentionPolicyVersion = workItem.RetentionPolicyVersion,
            OwnerReceiptContractVersion = workItem.OwnerReceiptContractVersion!.Value,
            OwnerReceiptId = workItem.OwnerReceiptId!.Value,
            OwnerReceiptSha256 = ownerReceiptDigest,
            PreviousEntrySha256 = previousDigest,
            ReplayOfLedgerEntryId = replayOfLedgerEntryId,
            SupersedesLedgerEntryId = supersedesLedgerEntryId
        };

        if (!entry.HasValidCoordinates())
        {
            return Invalid();
        }

        entry.EntrySha256 = entry.ComputeCanonicalSha256();
        return Result.Success(entry);
    }

    public bool HasValidCanonicalDigest() =>
        IsSha256(this.EntrySha256) &&
        string.Equals(
            this.EntrySha256,
            this.ComputeCanonicalSha256(),
            StringComparison.Ordinal);

    private bool HasValidCoordinates() =>
        this.ContractVersion == CurrentContractVersion &&
        this.RecordPseudonymKeyVersion > 0 &&
        IsSha256(this.RecordPseudonymSha256) &&
        this.PolicyEvidenceSchemaVersion ==
            DataRightsApprovalPolicyEvidence.CurrentSchemaVersion &&
        this.PolicyVersion > 0 &&
        this.RetentionPolicyVersion > 0 &&
        !string.IsNullOrWhiteSpace(this.PolicyId) &&
        this.PolicyId.Length <= DataRightsApprovalPolicyEvidence.KeyMaxLength &&
        !string.IsNullOrWhiteSpace(this.RetentionPolicyId) &&
        this.RetentionPolicyId.Length <= DataRightsApprovalPolicyEvidence.KeyMaxLength &&
        IsSha256(this.PolicyContentSha256);

    private string ComputeCanonicalSha256()
    {
        StringBuilder canonical = new();
        Append(canonical, this.ContractVersion.ToString(CultureInfo.InvariantCulture));
        Append(canonical, this.Id.ToString("N"));
        Append(canonical, this.ScopeId);
        Append(canonical, this.TenantSequence.ToString(CultureInfo.InvariantCulture));
        Append(canonical, this.WorkItemId.ToString("N"));
        Append(canonical, this.CaseId.ToString("N"));
        Append(canonical, this.ApprovalRevision.ToString(CultureInfo.InvariantCulture));
        Append(canonical, this.OperationRevision.ToString(CultureInfo.InvariantCulture));
        Append(canonical, ((int)this.Operation).ToString(CultureInfo.InvariantCulture));
        Append(canonical, this.RoutingPropertyId.ToString("N"));
        Append(canonical, this.OwnerKey);
        Append(canonical, this.RecordType);
        Append(
            canonical,
            this.RecordPseudonymKeyVersion.ToString(CultureInfo.InvariantCulture));
        Append(canonical, this.RecordPseudonymSha256);
        Append(canonical, this.DispositionCode);
        Append(canonical, this.ReasonCode);
        Append(
            canonical,
            this.CompletedAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        Append(
            canonical,
            this.PolicyEvidenceSchemaVersion.ToString(CultureInfo.InvariantCulture));
        Append(canonical, this.PolicyId);
        Append(canonical, this.PolicyVersion.ToString(CultureInfo.InvariantCulture));
        Append(canonical, this.PolicyContentSha256);
        Append(canonical, this.RetentionPolicyId);
        Append(
            canonical,
            this.RetentionPolicyVersion.ToString(CultureInfo.InvariantCulture));
        Append(
            canonical,
            this.OwnerReceiptContractVersion.ToString(CultureInfo.InvariantCulture));
        Append(canonical, this.OwnerReceiptId.ToString("N"));
        Append(canonical, this.OwnerReceiptSha256);
        Append(canonical, this.PreviousEntrySha256);
        Append(canonical, Coordinate(this.ReplayOfLedgerEntryId));
        Append(canonical, Coordinate(this.SupersedesLedgerEntryId));
        return Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())));
    }

    private static string Coordinate(Guid? value) =>
        value.HasValue ? value.Value.ToString("N") : "-";

    private static void Append(StringBuilder target, string value)
    {
        target.Append(value.Length.ToString(CultureInfo.InvariantCulture));
        target.Append(':');
        target.Append(value);
    }

    private static string NormalizeSha256(string? value) =>
        value?.Trim().ToLowerInvariant() ?? string.Empty;

    private static bool IsSha256(string value) =>
        value.Length == Sha256Length &&
        value.All(character =>
            character is (>= '0' and <= '9') or (>= 'a' and <= 'f'));

    private static Result<DataRightsProcessingLedgerEntry> Invalid() =>
        Result.Failure<DataRightsProcessingLedgerEntry>(
            DataRightsDomainErrors.ProcessingLedgerEntryInvalid);
}

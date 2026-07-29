namespace BunkFy.Modules.DataRights.Domain.Entities;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Errors;
using BunkFy.Modules.DataRights.Domain.Models;
using BunkFy.Modules.DataRights.Domain.ValueObjects;
using Gma.Framework.Domain.Models;
using Gma.Framework.Naming;
using Gma.Framework.Results;

public sealed class DataRightsProcessingLedgerEntry : ScopedEntity<Guid>
{
    public const int MinimumSupportedContractVersion = 1;
    public const int GuestResultVersionContractVersion = 2;
    public const int CurrentContractVersion = 3;
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
    public DataRightsCaseKind CaseKind { get; private set; }
    public DataRightsCaseScopeKind ScopeKind { get; private set; }
    public Guid? RoutingPropertyId { get; private set; }
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
    public long? PolicyPropertyVersion { get; private set; }
    public string? PolicyOperatingCountryCode { get; private set; }
    public string? PolicyPurposeCode { get; private set; }
    public string? PolicySurface { get; private set; }
    public string? PolicySourceProvenance { get; private set; }
    public string? PolicyRetentionDataClass { get; private set; }
    public string? PolicyRetentionTrigger { get; private set; }
    public DateTimeOffset? PolicyRetentionTriggeredAtUtc { get; private set; }
    public DateTimeOffset? PolicyRetentionDeadlineUtc { get; private set; }
    public DateTimeOffset? PolicyEvaluatedAtUtc { get; private set; }
    public string? PolicyStateBindingsJson { get; private set; }
    public string? PolicyStateBindingsSha256 { get; private set; }
    public bool? PolicyRequiresDistinctExecutor { get; private set; }
    public int OwnerReceiptContractVersion { get; private set; }
    public Guid OwnerReceiptId { get; private set; }
    public string OwnerReceiptSha256 { get; private set; } = string.Empty;
    public long? ResultingRecordVersion { get; private set; }
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
            DataRightsExecutionScope.Create(
                workItem.CaseKind,
                workItem.ScopeKind,
                workItem.PropertyId).IsFailure ||
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

        bool useGuestCompatibilityContract =
            workItem.CaseKind == DataRightsCaseKind.GuestRights &&
            workItem.ScopeKind == DataRightsCaseScopeKind.Property &&
            workItem.PropertyId is Guid propertyId &&
            propertyId != Guid.Empty &&
            workItem.PolicyEvidenceSchemaVersion ==
                DataRightsApprovalPolicyEvidence.MinimumSupportedSchemaVersion;
        int contractVersion = useGuestCompatibilityContract
            ? GuestResultVersionContractVersion
            : CurrentContractVersion;
        bool freezeScopedEvidence =
            contractVersion == CurrentContractVersion;

        DataRightsProcessingLedgerEntry entry = new(entryId, workItem.ScopeId)
        {
            ContractVersion = contractVersion,
            TenantSequence = tenantSequence,
            WorkItemId = workItem.Id,
            CaseId = workItem.CaseId,
            ApprovalRevision = workItem.ApprovalRevision,
            OperationRevision = workItem.ExecutionRevision,
            Operation = workItem.Operation,
            CaseKind = freezeScopedEvidence
                ? workItem.CaseKind
                : DataRightsCaseKind.Unknown,
            ScopeKind = freezeScopedEvidence
                ? workItem.ScopeKind
                : DataRightsCaseScopeKind.Unknown,
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
            PolicyPropertyVersion = freezeScopedEvidence
                ? workItem.PolicyPropertyVersion
                : null,
            PolicyOperatingCountryCode = freezeScopedEvidence
                ? workItem.PolicyOperatingCountryCode
                : null,
            PolicyPurposeCode = freezeScopedEvidence
                ? workItem.PolicyPurposeCode
                : null,
            PolicySurface = freezeScopedEvidence
                ? workItem.PolicySurface
                : null,
            PolicySourceProvenance = freezeScopedEvidence
                ? workItem.PolicySourceProvenance
                : null,
            PolicyRetentionDataClass = freezeScopedEvidence
                ? workItem.PolicyRetentionDataClass
                : null,
            PolicyRetentionTrigger = freezeScopedEvidence
                ? workItem.PolicyRetentionTrigger
                : null,
            PolicyRetentionTriggeredAtUtc = freezeScopedEvidence
                ? workItem.PolicyRetentionTriggeredAtUtc
                : null,
            PolicyRetentionDeadlineUtc = freezeScopedEvidence
                ? workItem.PolicyRetentionDeadlineUtc
                : null,
            PolicyEvaluatedAtUtc = freezeScopedEvidence
                ? workItem.PolicyEvaluatedAtUtc
                : null,
            PolicyStateBindingsJson = freezeScopedEvidence
                ? workItem.PolicyStateBindingsJson
                : null,
            PolicyStateBindingsSha256 = freezeScopedEvidence
                ? workItem.PolicyStateBindingsSha256
                : null,
            PolicyRequiresDistinctExecutor = freezeScopedEvidence
                ? workItem.PolicyRequiresDistinctExecutor
                : null,
            OwnerReceiptContractVersion = workItem.OwnerReceiptContractVersion!.Value,
            OwnerReceiptId = workItem.OwnerReceiptId!.Value,
            OwnerReceiptSha256 = ownerReceiptDigest,
            ResultingRecordVersion = workItem.ResultingRecordVersion,
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
        this.HasValidCoordinates() &&
        IsSha256(this.EntrySha256) &&
        string.Equals(
            this.EntrySha256,
            this.ComputeCanonicalSha256(),
            StringComparison.Ordinal);

    public bool MatchesExecutionProof(DataRightsExecutionWorkItem workItem)
    {
        ArgumentNullException.ThrowIfNull(workItem);

        bool matchesResultVersion = this.ContractVersion == 1
            ? this.ResultingRecordVersion is null
            : this.ResultingRecordVersion == workItem.ResultingRecordVersion;
        bool matchesCompatibilityContract =
            this.ContractVersion is 1 or GuestResultVersionContractVersion
                ? workItem.CaseKind == DataRightsCaseKind.GuestRights &&
                  workItem.ScopeKind == DataRightsCaseScopeKind.Property &&
                  workItem.PolicyEvidenceSchemaVersion ==
                    DataRightsApprovalPolicyEvidence.MinimumSupportedSchemaVersion &&
                  this.CaseKind == DataRightsCaseKind.Unknown &&
                  this.ScopeKind == DataRightsCaseScopeKind.Unknown
                : this.ContractVersion == CurrentContractVersion &&
                  this.CaseKind == workItem.CaseKind &&
                  this.ScopeKind == workItem.ScopeKind &&
                  this.PolicyPropertyVersion == workItem.PolicyPropertyVersion &&
                  EqualsOrdinal(
                      this.PolicyOperatingCountryCode,
                      workItem.PolicyOperatingCountryCode) &&
                  EqualsOrdinal(
                      this.PolicyPurposeCode,
                      workItem.PolicyPurposeCode) &&
                  EqualsOrdinal(this.PolicySurface, workItem.PolicySurface) &&
                  EqualsOrdinal(
                      this.PolicySourceProvenance,
                      workItem.PolicySourceProvenance) &&
                  EqualsOrdinal(
                      this.PolicyRetentionDataClass,
                      workItem.PolicyRetentionDataClass) &&
                  EqualsOrdinal(
                      this.PolicyRetentionTrigger,
                      workItem.PolicyRetentionTrigger) &&
                  this.PolicyRetentionTriggeredAtUtc ==
                    workItem.PolicyRetentionTriggeredAtUtc &&
                  this.PolicyRetentionDeadlineUtc ==
                    workItem.PolicyRetentionDeadlineUtc &&
                  this.PolicyEvaluatedAtUtc == workItem.PolicyEvaluatedAtUtc &&
                  EqualsOrdinal(
                      this.PolicyStateBindingsJson,
                      workItem.PolicyStateBindingsJson) &&
                  EqualsOrdinal(
                      this.PolicyStateBindingsSha256,
                      workItem.PolicyStateBindingsSha256) &&
                  this.PolicyRequiresDistinctExecutor ==
                    workItem.PolicyRequiresDistinctExecutor;

        return this.HasValidCoordinates() &&
            this.HasValidCanonicalDigest() &&
            matchesResultVersion &&
            matchesCompatibilityContract &&
            this.ScopeId == workItem.ScopeId &&
            this.WorkItemId == workItem.Id &&
            this.CaseId == workItem.CaseId &&
            this.ApprovalRevision == workItem.ApprovalRevision &&
            this.OperationRevision == workItem.ExecutionRevision &&
            this.Operation == workItem.Operation &&
            this.RoutingPropertyId == workItem.PropertyId &&
            EqualsOrdinal(this.OwnerKey, workItem.OwnerKey) &&
            EqualsOrdinal(this.RecordType, workItem.RecordType) &&
            EqualsOrdinal(this.DispositionCode, workItem.OwnerDispositionCode) &&
            EqualsOrdinal(this.ReasonCode, workItem.OwnerReasonCode) &&
            this.CompletedAtUtc ==
                workItem.OwnerCompletedAtUtc?.ToUniversalTime() &&
            this.PolicyEvidenceSchemaVersion ==
                workItem.PolicyEvidenceSchemaVersion &&
            EqualsOrdinal(this.PolicyId, workItem.PolicyId) &&
            this.PolicyVersion == workItem.PolicyVersion &&
            EqualsOrdinal(
                this.PolicyContentSha256,
                workItem.PolicyContentSha256) &&
            EqualsOrdinal(this.RetentionPolicyId, workItem.RetentionPolicyId) &&
            this.RetentionPolicyVersion == workItem.RetentionPolicyVersion &&
            this.OwnerReceiptContractVersion ==
                workItem.OwnerReceiptContractVersion &&
            this.OwnerReceiptId == workItem.OwnerReceiptId &&
            EqualsOrdinal(
                this.OwnerReceiptSha256,
                workItem.OwnerReceiptSha256);
    }

    public DataRightsProcessingLedgerSnapshot Freeze() =>
        new(
            this.ContractVersion,
            this.Id,
            this.ScopeId,
            this.TenantSequence,
            this.WorkItemId,
            this.CaseId,
            this.ApprovalRevision,
            this.OperationRevision,
            this.Operation,
            this.RoutingPropertyId,
            this.OwnerKey,
            this.RecordType,
            this.RecordPseudonymKeyVersion,
            this.RecordPseudonymSha256,
            this.DispositionCode,
            this.ReasonCode,
            this.CompletedAtUtc,
            this.PolicyEvidenceSchemaVersion,
            this.PolicyId,
            this.PolicyVersion,
            this.PolicyContentSha256,
            this.RetentionPolicyId,
            this.RetentionPolicyVersion,
            this.OwnerReceiptContractVersion,
            this.OwnerReceiptId,
            this.OwnerReceiptSha256,
            this.PreviousEntrySha256,
            this.EntrySha256,
            this.ReplayOfLedgerEntryId,
            this.SupersedesLedgerEntryId,
            this.ResultingRecordVersion,
            this.CaseKind,
            this.ScopeKind,
            this.PolicyPropertyVersion,
            this.PolicyOperatingCountryCode,
            this.PolicyPurposeCode,
            this.PolicySurface,
            this.PolicySourceProvenance,
            this.PolicyRetentionDataClass,
            this.PolicyRetentionTrigger,
            this.PolicyRetentionTriggeredAtUtc,
            this.PolicyRetentionDeadlineUtc,
            this.PolicyEvaluatedAtUtc,
            this.PolicyStateBindingsJson,
            this.PolicyStateBindingsSha256,
            this.PolicyRequiresDistinctExecutor);

    public static Result<DataRightsProcessingLedgerEntry> Restore(
        DataRightsProcessingLedgerSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        DataRightsProcessingLedgerEntry entry = new(snapshot.EntryId, snapshot.ScopeId)
        {
            ContractVersion = snapshot.ContractVersion,
            TenantSequence = snapshot.TenantSequence,
            WorkItemId = snapshot.WorkItemId,
            CaseId = snapshot.CaseId,
            ApprovalRevision = snapshot.ApprovalRevision,
            OperationRevision = snapshot.OperationRevision,
            Operation = snapshot.Operation,
            CaseKind = snapshot.CaseKind,
            ScopeKind = snapshot.ScopeKind,
            RoutingPropertyId = snapshot.RoutingPropertyId,
            OwnerKey = snapshot.OwnerKey,
            RecordType = snapshot.RecordType,
            RecordPseudonymKeyVersion = snapshot.RecordPseudonymKeyVersion,
            RecordPseudonymSha256 = snapshot.RecordPseudonymSha256,
            DispositionCode = snapshot.DispositionCode,
            ReasonCode = snapshot.ReasonCode,
            CompletedAtUtc = snapshot.CompletedAtUtc,
            PolicyEvidenceSchemaVersion = snapshot.PolicyEvidenceSchemaVersion,
            PolicyId = snapshot.PolicyId,
            PolicyVersion = snapshot.PolicyVersion,
            PolicyContentSha256 = snapshot.PolicyContentSha256,
            RetentionPolicyId = snapshot.RetentionPolicyId,
            RetentionPolicyVersion = snapshot.RetentionPolicyVersion,
            PolicyPropertyVersion = snapshot.PolicyPropertyVersion,
            PolicyOperatingCountryCode =
                snapshot.PolicyOperatingCountryCode,
            PolicyPurposeCode = snapshot.PolicyPurposeCode,
            PolicySurface = snapshot.PolicySurface,
            PolicySourceProvenance = snapshot.PolicySourceProvenance,
            PolicyRetentionDataClass =
                snapshot.PolicyRetentionDataClass,
            PolicyRetentionTrigger = snapshot.PolicyRetentionTrigger,
            PolicyRetentionTriggeredAtUtc =
                snapshot.PolicyRetentionTriggeredAtUtc,
            PolicyRetentionDeadlineUtc =
                snapshot.PolicyRetentionDeadlineUtc,
            PolicyEvaluatedAtUtc = snapshot.PolicyEvaluatedAtUtc,
            PolicyStateBindingsJson = snapshot.PolicyStateBindingsJson,
            PolicyStateBindingsSha256 =
                snapshot.PolicyStateBindingsSha256,
            PolicyRequiresDistinctExecutor =
                snapshot.PolicyRequiresDistinctExecutor,
            OwnerReceiptContractVersion = snapshot.OwnerReceiptContractVersion,
            OwnerReceiptId = snapshot.OwnerReceiptId,
            OwnerReceiptSha256 = snapshot.OwnerReceiptSha256,
            ResultingRecordVersion = snapshot.ResultingRecordVersion,
            PreviousEntrySha256 = snapshot.PreviousEntrySha256,
            EntrySha256 = snapshot.EntrySha256,
            ReplayOfLedgerEntryId = snapshot.ReplayOfLedgerEntryId,
            SupersedesLedgerEntryId = snapshot.SupersedesLedgerEntryId
        };

        return entry.HasValidCoordinates() && entry.HasValidCanonicalDigest()
            ? Result.Success(entry)
            : Invalid();
    }

    private bool HasValidCoordinates() =>
        this.ContractVersion is >= MinimumSupportedContractVersion
            and <= CurrentContractVersion &&
        this.Id != Guid.Empty &&
        TenantIds.TryNormalize(this.ScopeId, out string? normalizedScopeId) &&
        string.Equals(this.ScopeId, normalizedScopeId, StringComparison.Ordinal) &&
        this.TenantSequence > 0 &&
        this.WorkItemId != Guid.Empty &&
        this.CaseId != Guid.Empty &&
        this.ApprovalRevision > 0 &&
        this.OperationRevision > this.ApprovalRevision &&
        this.Operation == DataRightsCaseOperation.Anonymisation &&
        this.HasValidScopeContract() &&
        HasCanonicalCode(
            this.OwnerKey,
            DataRightsSubjectCoordinate.OwnerKeyMaxLength) &&
        HasCanonicalCode(
            this.RecordType,
            DataRightsSubjectCoordinate.RecordTypeMaxLength) &&
        this.RecordPseudonymKeyVersion > 0 &&
        IsSha256(this.RecordPseudonymSha256) &&
        HasCanonicalCode(this.DispositionCode, CodeMaxLength) &&
        HasCanonicalCode(this.ReasonCode, CodeMaxLength) &&
        this.CompletedAtUtc != default &&
        this.CompletedAtUtc.Offset == TimeSpan.Zero &&
        this.HasValidPolicyContract() &&
        this.PolicyVersion > 0 &&
        this.RetentionPolicyVersion > 0 &&
        HasCanonicalCode(
            this.PolicyId,
            DataRightsApprovalPolicyEvidence.KeyMaxLength) &&
        HasCanonicalCode(
            this.RetentionPolicyId,
            DataRightsApprovalPolicyEvidence.KeyMaxLength) &&
        IsSha256(this.PolicyContentSha256) &&
        this.OwnerReceiptContractVersion > 0 &&
        this.OwnerReceiptId != Guid.Empty &&
        IsSha256(this.OwnerReceiptSha256) &&
        (this.ContractVersion == MinimumSupportedContractVersion
            ? !this.ResultingRecordVersion.HasValue
            : this.ResultingRecordVersion > 0) &&
        IsSha256(this.PreviousEntrySha256) &&
        (this.TenantSequence == 1
            ? string.Equals(
                this.PreviousEntrySha256,
                GenesisEntrySha256,
                StringComparison.Ordinal)
            : !string.Equals(
                this.PreviousEntrySha256,
                GenesisEntrySha256,
                StringComparison.Ordinal)) &&
        this.ReplayOfLedgerEntryId != this.Id &&
        this.SupersedesLedgerEntryId != this.Id &&
        (!this.ReplayOfLedgerEntryId.HasValue ||
         !this.SupersedesLedgerEntryId.HasValue ||
         this.ReplayOfLedgerEntryId != this.SupersedesLedgerEntryId);

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
        if (this.ContractVersion < CurrentContractVersion)
        {
            Append(canonical, this.RoutingPropertyId!.Value.ToString("N"));
        }
        else
        {
            Append(
                canonical,
                ((int)this.CaseKind).ToString(CultureInfo.InvariantCulture));
            Append(
                canonical,
                ((int)this.ScopeKind).ToString(CultureInfo.InvariantCulture));
            Append(canonical, Coordinate(this.RoutingPropertyId));
        }
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
        if (this.ContractVersion >= GuestResultVersionContractVersion)
        {
            Append(
                canonical,
                this.ResultingRecordVersion!.Value.ToString(
                    CultureInfo.InvariantCulture));
        }

        if (this.ContractVersion >= CurrentContractVersion)
        {
            Append(
                canonical,
                this.PolicyPropertyVersion!.Value.ToString(
                    CultureInfo.InvariantCulture));
            Append(canonical, this.PolicyOperatingCountryCode!);
            Append(canonical, this.PolicyPurposeCode!);
            Append(canonical, this.PolicySurface!);
            Append(canonical, this.PolicySourceProvenance!);
            Append(canonical, this.PolicyRetentionDataClass!);
            Append(canonical, this.PolicyRetentionTrigger!);
            Append(
                canonical,
                Timestamp(this.PolicyRetentionTriggeredAtUtc));
            Append(
                canonical,
                Timestamp(this.PolicyRetentionDeadlineUtc));
            Append(canonical, Timestamp(this.PolicyEvaluatedAtUtc));
            Append(canonical, this.PolicyStateBindingsJson!);
            Append(canonical, this.PolicyStateBindingsSha256!);
            Append(
                canonical,
                this.PolicyRequiresDistinctExecutor == true ? "1" : "0");
        }

        Append(canonical, this.PreviousEntrySha256);
        Append(canonical, Coordinate(this.ReplayOfLedgerEntryId));
        Append(canonical, Coordinate(this.SupersedesLedgerEntryId));
        return Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())));
    }

    private static string Coordinate(Guid? value) =>
        value.HasValue ? value.Value.ToString("N") : "-";

    private static string Timestamp(DateTimeOffset? value) =>
        value.HasValue
            ? value.Value.ToUniversalTime().ToString(
                "O",
                CultureInfo.InvariantCulture)
            : "-";

    private bool HasValidScopeContract()
    {
        if (this.ContractVersion < CurrentContractVersion)
        {
            return this.CaseKind == DataRightsCaseKind.Unknown &&
                this.ScopeKind == DataRightsCaseScopeKind.Unknown &&
                this.RoutingPropertyId is Guid propertyId &&
                propertyId != Guid.Empty;
        }

        return DataRightsExecutionScope.Create(
            this.CaseKind,
            this.ScopeKind,
            this.RoutingPropertyId).IsSuccess;
    }

    private bool HasValidPolicyContract()
    {
        if (this.ContractVersion < CurrentContractVersion)
        {
            return this.PolicyEvidenceSchemaVersion ==
                    DataRightsApprovalPolicyEvidence
                        .MinimumSupportedSchemaVersion &&
                this.PolicyPropertyVersion is null &&
                this.PolicyOperatingCountryCode is null &&
                this.PolicyPurposeCode is null &&
                this.PolicySurface is null &&
                this.PolicySourceProvenance is null &&
                this.PolicyRetentionDataClass is null &&
                this.PolicyRetentionTrigger is null &&
                this.PolicyRetentionTriggeredAtUtc is null &&
                this.PolicyRetentionDeadlineUtc is null &&
                this.PolicyEvaluatedAtUtc is null &&
                this.PolicyStateBindingsJson is null &&
                this.PolicyStateBindingsSha256 is null &&
                this.PolicyRequiresDistinctExecutor is null;
        }

        bool propertyVersionValid = this.ScopeKind switch
        {
            DataRightsCaseScopeKind.Property =>
                this.PolicyPropertyVersion > 0,
            DataRightsCaseScopeKind.Tenant =>
                this.PolicyPropertyVersion == 0,
            _ => false
        };
        return this.PolicyEvidenceSchemaVersion ==
                DataRightsApprovalPolicyEvidence.CurrentSchemaVersion &&
            propertyVersionValid &&
            this.PolicyOperatingCountryCode is
            {
                Length: DataRightsApprovalPolicyEvidence.CountryCodeLength
            } country &&
            country.All(character => character is >= 'A' and <= 'Z') &&
            HasCanonicalCode(
                this.PolicyPurposeCode,
                DataRightsApprovalPolicyEvidence.KeyMaxLength) &&
            HasCanonicalCode(
                this.PolicySurface,
                DataRightsApprovalPolicyEvidence.KeyMaxLength) &&
            HasCanonicalCode(
                this.PolicySourceProvenance,
                DataRightsApprovalPolicyEvidence.KeyMaxLength) &&
            HasCanonicalCode(
                this.PolicyRetentionDataClass,
                DataRightsApprovalPolicyEvidence.KeyMaxLength) &&
            HasCanonicalCode(
                this.PolicyRetentionTrigger,
                DataRightsApprovalPolicyEvidence.KeyMaxLength) &&
            this.PolicyRetentionTriggeredAtUtc is DateTimeOffset triggered &&
            triggered.Offset == TimeSpan.Zero &&
            this.PolicyRetentionDeadlineUtc is DateTimeOffset deadline &&
            deadline.Offset == TimeSpan.Zero &&
            deadline > triggered &&
            this.PolicyEvaluatedAtUtc is DateTimeOffset evaluated &&
            evaluated.Offset == TimeSpan.Zero &&
            deadline <= evaluated &&
            this.PolicyStateBindingsJson is not null &&
            this.PolicyStateBindingsSha256 is not null &&
            DataRightsApprovalPolicyEvidence.HasValidStateBindings(
                this.PolicyStateBindingsJson,
                this.PolicyStateBindingsSha256) &&
            this.PolicyRequiresDistinctExecutor == true;
    }

    private static void Append(StringBuilder target, string value)
    {
        target.Append(value.Length.ToString(CultureInfo.InvariantCulture));
        target.Append(':');
        target.Append(value);
    }

    private static string NormalizeSha256(string? value) =>
        value?.Trim().ToLowerInvariant() ?? string.Empty;

    private static bool IsSha256(string? value) =>
        value is not null &&
        value.Length == Sha256Length &&
        value.All(character =>
            character is (>= '0' and <= '9') or (>= 'a' and <= 'f'));

    private static bool EqualsOrdinal(string? left, string? right) =>
        string.Equals(left, right, StringComparison.Ordinal);

    private static bool HasCanonicalCode(string? value, int maxLength) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Length <= maxLength &&
        string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private static Result<DataRightsProcessingLedgerEntry> Invalid() =>
        Result.Failure<DataRightsProcessingLedgerEntry>(
            DataRightsDomainErrors.ProcessingLedgerEntryInvalid);
}

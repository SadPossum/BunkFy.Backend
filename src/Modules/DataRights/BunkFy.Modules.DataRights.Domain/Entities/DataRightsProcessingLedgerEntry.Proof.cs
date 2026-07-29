namespace BunkFy.Modules.DataRights.Domain.Entities;

using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Models;
using BunkFy.Modules.DataRights.Domain.ValueObjects;
using Gma.Framework.Results;

public sealed partial class DataRightsProcessingLedgerEntry
{
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
                    DataRightsApprovalPolicyEvidence
                        .MinimumSupportedSchemaVersion &&
                  this.CaseKind == DataRightsCaseKind.Unknown &&
                  this.ScopeKind == DataRightsCaseScopeKind.Unknown
                : this.ContractVersion == CurrentContractVersion &&
                  this.CaseKind == workItem.CaseKind &&
                  this.ScopeKind == workItem.ScopeKind &&
                  this.PolicyPropertyVersion ==
                    workItem.PolicyPropertyVersion &&
                  EqualsOrdinal(
                      this.PolicyOperatingCountryCode,
                      workItem.PolicyOperatingCountryCode) &&
                  EqualsOrdinal(
                      this.PolicyPurposeCode,
                      workItem.PolicyPurposeCode) &&
                  EqualsOrdinal(
                      this.PolicySurface,
                      workItem.PolicySurface) &&
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
                  this.PolicyEvaluatedAtUtc ==
                    workItem.PolicyEvaluatedAtUtc &&
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
            EqualsOrdinal(
                this.DispositionCode,
                workItem.OwnerDispositionCode) &&
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
            EqualsOrdinal(
                this.RetentionPolicyId,
                workItem.RetentionPolicyId) &&
            this.RetentionPolicyVersion ==
                workItem.RetentionPolicyVersion &&
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

        DataRightsProcessingLedgerEntry entry =
            new(snapshot.EntryId, snapshot.ScopeId)
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
                RecordPseudonymKeyVersion =
                    snapshot.RecordPseudonymKeyVersion,
                RecordPseudonymSha256 =
                    snapshot.RecordPseudonymSha256,
                DispositionCode = snapshot.DispositionCode,
                ReasonCode = snapshot.ReasonCode,
                CompletedAtUtc = snapshot.CompletedAtUtc,
                PolicyEvidenceSchemaVersion =
                    snapshot.PolicyEvidenceSchemaVersion,
                PolicyId = snapshot.PolicyId,
                PolicyVersion = snapshot.PolicyVersion,
                PolicyContentSha256 =
                    snapshot.PolicyContentSha256,
                RetentionPolicyId = snapshot.RetentionPolicyId,
                RetentionPolicyVersion =
                    snapshot.RetentionPolicyVersion,
                PolicyPropertyVersion =
                    snapshot.PolicyPropertyVersion,
                PolicyOperatingCountryCode =
                    snapshot.PolicyOperatingCountryCode,
                PolicyPurposeCode = snapshot.PolicyPurposeCode,
                PolicySurface = snapshot.PolicySurface,
                PolicySourceProvenance =
                    snapshot.PolicySourceProvenance,
                PolicyRetentionDataClass =
                    snapshot.PolicyRetentionDataClass,
                PolicyRetentionTrigger =
                    snapshot.PolicyRetentionTrigger,
                PolicyRetentionTriggeredAtUtc =
                    snapshot.PolicyRetentionTriggeredAtUtc,
                PolicyRetentionDeadlineUtc =
                    snapshot.PolicyRetentionDeadlineUtc,
                PolicyEvaluatedAtUtc =
                    snapshot.PolicyEvaluatedAtUtc,
                PolicyStateBindingsJson =
                    snapshot.PolicyStateBindingsJson,
                PolicyStateBindingsSha256 =
                    snapshot.PolicyStateBindingsSha256,
                PolicyRequiresDistinctExecutor =
                    snapshot.PolicyRequiresDistinctExecutor,
                OwnerReceiptContractVersion =
                    snapshot.OwnerReceiptContractVersion,
                OwnerReceiptId = snapshot.OwnerReceiptId,
                OwnerReceiptSha256 =
                    snapshot.OwnerReceiptSha256,
                ResultingRecordVersion =
                    snapshot.ResultingRecordVersion,
                PreviousEntrySha256 =
                    snapshot.PreviousEntrySha256,
                EntrySha256 = snapshot.EntrySha256,
                ReplayOfLedgerEntryId =
                    snapshot.ReplayOfLedgerEntryId,
                SupersedesLedgerEntryId =
                    snapshot.SupersedesLedgerEntryId
            };

        return entry.HasValidCoordinates() &&
            entry.HasValidCanonicalDigest()
                ? Result.Success(entry)
                : Invalid();
    }
}

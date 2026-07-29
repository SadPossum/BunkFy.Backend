namespace BunkFy.Modules.DataRights.Domain.Aggregates;

using BunkFy.Modules.DataRights.Domain.Errors;
using BunkFy.Modules.DataRights.Domain.Models;
using BunkFy.Modules.DataRights.Domain.ValueObjects;
using Gma.Framework.Results;

public sealed partial class DataRightsExecutionWorkItem
{
    public bool MatchesPolicyEvidence(
        DataRightsApprovalPolicyEvidence evidence) =>
        evidence is not null &&
        evidence.HasValidShape() &&
        this.PolicyEvidenceSchemaVersion == evidence.SchemaVersion &&
        this.CaseKind == evidence.CaseKind &&
        this.ScopeKind == evidence.ScopeKind &&
        this.PropertyId == evidence.PropertyId &&
        this.PolicyPropertyVersion == evidence.PropertyVersion &&
        string.Equals(
            this.PolicyOperatingCountryCode,
            evidence.OperatingCountryCode,
            StringComparison.Ordinal) &&
        string.Equals(
            this.PolicyId,
            evidence.PolicyId,
            StringComparison.Ordinal) &&
        this.PolicyVersion == evidence.PolicyVersion &&
        string.Equals(
            this.RetentionPolicyId,
            evidence.RetentionPolicyId,
            StringComparison.Ordinal) &&
        this.RetentionPolicyVersion == evidence.RetentionPolicyVersion &&
        string.Equals(
            this.PolicyContentSha256,
            evidence.ContentSha256,
            StringComparison.Ordinal) &&
        string.Equals(
            this.PolicyPurposeCode,
            evidence.PurposeCode,
            StringComparison.Ordinal) &&
        string.Equals(
            this.PolicySurface,
            evidence.Surface,
            StringComparison.Ordinal) &&
        string.Equals(
            this.PolicySourceProvenance,
            evidence.SourceProvenance,
            StringComparison.Ordinal) &&
        string.Equals(
            this.PolicyRetentionDataClass,
            evidence.RetentionDataClass,
            StringComparison.Ordinal) &&
        string.Equals(
            this.PolicyRetentionTrigger,
            evidence.RetentionTrigger,
            StringComparison.Ordinal) &&
        this.PolicyRetentionTriggeredAtUtc ==
            evidence.RetentionTriggeredAtUtc &&
        this.PolicyRetentionDeadlineUtc ==
            evidence.RetentionDeadlineUtc &&
        this.PolicyEvaluatedAtUtc == evidence.EvaluatedAtUtc &&
        string.Equals(
            this.PolicyStateBindingsJson,
            evidence.StateBindingsJson,
            StringComparison.Ordinal) &&
        string.Equals(
            this.PolicyStateBindingsSha256,
            evidence.StateBindingsSha256,
            StringComparison.Ordinal) &&
        this.PolicyRequiresDistinctExecutor ==
            evidence.RequiresDistinctExecutor;

    public Result RecordOwnerProof(
        long expectedVersion,
        Guid taskRunId,
        int taskAttempt,
        int receiptContractVersion,
        Guid receiptId,
        long resultingRecordVersion,
        string dispositionCode,
        string reasonCode,
        string receiptSha256,
        DateTimeOffset completedAtUtc,
        DateTimeOffset recordedAtUtc)
    {
        string normalizedDisposition =
            NormalizeCode(dispositionCode, OwnerCodeMaxLength);
        string normalizedReason =
            NormalizeCode(reasonCode, OwnerCodeMaxLength);
        string normalizedDigest =
            receiptSha256?.Trim().ToLowerInvariant() ?? string.Empty;

        if (this.State == DataRightsExecutionWorkItemState.OwnerProofRecorded)
        {
            return this.OwnerReceiptContractVersion ==
                    receiptContractVersion &&
                this.OwnerReceiptId == receiptId &&
                this.ResultingRecordVersion == resultingRecordVersion &&
                string.Equals(
                    this.OwnerDispositionCode,
                    normalizedDisposition,
                    StringComparison.Ordinal) &&
                string.Equals(
                    this.OwnerReasonCode,
                    normalizedReason,
                    StringComparison.Ordinal) &&
                string.Equals(
                    this.OwnerReceiptSha256,
                    normalizedDigest,
                    StringComparison.Ordinal) &&
                this.OwnerCompletedAtUtc == completedAtUtc
                    ? Result.Success()
                    : Result.Failure(
                        DataRightsDomainErrors.ExecutionOwnerResultInvalid);
        }

        if (!this.CanRecordResult(
                expectedVersion,
                taskRunId,
                taskAttempt) ||
            receiptContractVersion <= 0 ||
            receiptId == Guid.Empty ||
            resultingRecordVersion <= this.SelectedRecordVersion ||
            normalizedDisposition.Length == 0 ||
            normalizedReason.Length == 0 ||
            !IsSha256(normalizedDigest) ||
            completedAtUtc == default ||
            completedAtUtc < this.CreatedAtUtc ||
            recordedAtUtc == default ||
            recordedAtUtc < completedAtUtc)
        {
            return Result.Failure(
                DataRightsDomainErrors.ExecutionOwnerResultInvalid);
        }

        this.OwnerReceiptContractVersion = receiptContractVersion;
        this.OwnerReceiptId = receiptId;
        this.ResultingRecordVersion = resultingRecordVersion;
        this.OwnerDispositionCode = normalizedDisposition;
        this.OwnerReasonCode = normalizedReason;
        this.OwnerReceiptSha256 = normalizedDigest;
        this.OwnerCompletedAtUtc = completedAtUtc;
        this.OutcomeCode = null;
        this.OutcomeAtUtc = recordedAtUtc;
        this.State = DataRightsExecutionWorkItemState.OwnerProofRecorded;
        this.Version++;
        return Result.Success();
    }
}

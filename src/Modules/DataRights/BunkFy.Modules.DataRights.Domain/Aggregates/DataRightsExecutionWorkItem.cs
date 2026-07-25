namespace BunkFy.Modules.DataRights.Domain.Aggregates;

using BunkFy.Modules.DataRights.Domain.Entities;
using BunkFy.Modules.DataRights.Domain.Errors;
using BunkFy.Modules.DataRights.Domain.Models;
using BunkFy.Modules.DataRights.Domain.ValueObjects;
using Gma.Framework.Domain.Models;
using Gma.Framework.Naming;
using Gma.Framework.Results;

public sealed class DataRightsExecutionWorkItem : ScopedAggregateRoot<Guid>
{
    public const int CurrentOwnerContractVersion = 1;
    public const int OutcomeCodeMaxLength = 200;
    public const int OwnerCodeMaxLength = 100;
    public const int Sha256Length = 64;

    private DataRightsExecutionWorkItem() { }

    private DataRightsExecutionWorkItem(Guid id, string scopeId) : base(id, scopeId) { }

    public Guid IdempotencyKey { get; private set; }
    public Guid CaseId { get; private set; }
    public Guid PropertyId { get; private set; }
    public long ApprovalRevision { get; private set; }
    public long ExecutionRevision { get; private set; }
    public DataRightsCaseOperation Operation { get; private set; }
    public string OwnerKey { get; private set; } = string.Empty;
    public string RecordType { get; private set; } = string.Empty;
    public Guid RecordId { get; private set; }
    public long SelectedRecordVersion { get; private set; }
    public int PolicyEvidenceSchemaVersion { get; private set; }
    public string PolicyId { get; private set; } = string.Empty;
    public int PolicyVersion { get; private set; }
    public string RetentionPolicyId { get; private set; } = string.Empty;
    public int RetentionPolicyVersion { get; private set; }
    public string PolicyContentSha256 { get; private set; } = string.Empty;
    public int OwnerContractVersion { get; private set; }
    public DataRightsExecutionWorkItemState State { get; private set; }
    public int AttemptCount { get; private set; }
    public Guid? TaskRunId { get; private set; }
    public int LastTaskAttempt { get; private set; }
    public DateTimeOffset? LastAttemptAtUtc { get; private set; }
    public int? OwnerReceiptContractVersion { get; private set; }
    public Guid? OwnerReceiptId { get; private set; }
    public long? ResultingRecordVersion { get; private set; }
    public string? OwnerDispositionCode { get; private set; }
    public string? OwnerReasonCode { get; private set; }
    public string? OwnerReceiptSha256 { get; private set; }
    public DateTimeOffset? OwnerCompletedAtUtc { get; private set; }
    public string? OutcomeCode { get; private set; }
    public DateTimeOffset? OutcomeAtUtc { get; private set; }
    public string CreatedBy { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public long Version { get; private set; } = 1;

    public static Result<DataRightsExecutionWorkItem> Prepare(
        Guid id,
        string tenantId,
        Guid idempotencyKey,
        Guid caseId,
        Guid propertyId,
        long approvalRevision,
        long executionRevision,
        DataRightsCaseOperation operation,
        DataRightsSubjectCoordinate subject,
        DataRightsApprovalPolicyEvidence policyEvidence,
        string actorId,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(policyEvidence);

        if (id == Guid.Empty ||
            idempotencyKey == Guid.Empty ||
            caseId == Guid.Empty ||
            propertyId == Guid.Empty ||
            approvalRevision <= 0 ||
            executionRevision <= approvalRevision ||
            operation != DataRightsCaseOperation.Anonymisation)
        {
            return Result.Failure<DataRightsExecutionWorkItem>(
                DataRightsDomainErrors.ExecutionCoordinateInvalid);
        }

        if (!TenantIds.TryNormalize(tenantId, out string? scopeId))
        {
            return Result.Failure<DataRightsExecutionWorkItem>(
                DataRightsDomainErrors.TenantInvalid);
        }

        string normalizedActor = actorId?.Trim() ?? string.Empty;
        if (normalizedActor.Length is 0 or > DataRightsCase.ActorIdMaxLength)
        {
            return Result.Failure<DataRightsExecutionWorkItem>(
                DataRightsDomainErrors.ActorInvalid);
        }

        if (nowUtc == default ||
            policyEvidence.PropertyId != propertyId ||
            policyEvidence.SchemaVersion != DataRightsApprovalPolicyEvidence.CurrentSchemaVersion)
        {
            return Result.Failure<DataRightsExecutionWorkItem>(
                DataRightsDomainErrors.ExecutionCoordinateInvalid);
        }

        return Result.Success(new DataRightsExecutionWorkItem(id, scopeId)
        {
            IdempotencyKey = idempotencyKey,
            CaseId = caseId,
            PropertyId = propertyId,
            ApprovalRevision = approvalRevision,
            ExecutionRevision = executionRevision,
            Operation = operation,
            OwnerKey = subject.OwnerKey,
            RecordType = subject.RecordType,
            RecordId = subject.RecordId,
            SelectedRecordVersion = subject.RecordVersion,
            PolicyEvidenceSchemaVersion = policyEvidence.SchemaVersion,
            PolicyId = policyEvidence.PolicyId,
            PolicyVersion = policyEvidence.PolicyVersion,
            RetentionPolicyId = policyEvidence.RetentionPolicyId,
            RetentionPolicyVersion = policyEvidence.RetentionPolicyVersion,
            PolicyContentSha256 = policyEvidence.ContentSha256,
            OwnerContractVersion = CurrentOwnerContractVersion,
            State = DataRightsExecutionWorkItemState.Prepared,
            CreatedBy = normalizedActor,
            CreatedAtUtc = nowUtc
        });
    }

    public bool HasIdempotencyKey(Guid idempotencyKey) =>
        idempotencyKey != Guid.Empty && this.IdempotencyKey == idempotencyKey;

    public bool HasExecutionCoordinates(
        Guid caseId,
        Guid propertyId,
        long approvalRevision,
        long executionRevision) =>
        caseId != Guid.Empty &&
        propertyId != Guid.Empty &&
        this.CaseId == caseId &&
        this.PropertyId == propertyId &&
        this.ApprovalRevision == approvalRevision &&
        this.ExecutionRevision == executionRevision;

    public bool IsOwnerDispatchTerminal =>
        this.State is DataRightsExecutionWorkItemState.OwnerProofRecorded
            or DataRightsExecutionWorkItemState.Blocked
            or DataRightsExecutionWorkItemState.Failed
            or DataRightsExecutionWorkItemState.Completed
            or DataRightsExecutionWorkItemState.NoOp;

    public Result BeginProcessing(
        Guid taskRunId,
        int taskAttempt,
        DateTimeOffset nowUtc)
    {
        if (this.IsOwnerDispatchTerminal)
        {
            return Result.Success();
        }

        if (taskRunId == Guid.Empty ||
            taskAttempt <= 0 ||
            nowUtc == default ||
            nowUtc < this.CreatedAtUtc ||
            this.OwnerContractVersion != CurrentOwnerContractVersion)
        {
            return Result.Failure(DataRightsDomainErrors.ExecutionCoordinateInvalid);
        }

        if (this.State == DataRightsExecutionWorkItemState.Prepared)
        {
            if (this.TaskRunId.HasValue ||
                this.LastTaskAttempt != 0 ||
                this.LastAttemptAtUtc.HasValue)
            {
                return Result.Failure(DataRightsDomainErrors.ExecutionTaskConflict);
            }

            this.TaskRunId = taskRunId;
        }
        else if (this.State != DataRightsExecutionWorkItemState.Processing ||
            this.TaskRunId != taskRunId)
        {
            return Result.Failure(DataRightsDomainErrors.ExecutionTaskConflict);
        }

        if (this.LastTaskAttempt == taskAttempt)
        {
            return Result.Success();
        }

        if (taskAttempt < this.LastTaskAttempt)
        {
            return Result.Failure(DataRightsDomainErrors.ExecutionTaskConflict);
        }

        this.State = DataRightsExecutionWorkItemState.Processing;
        this.LastTaskAttempt = taskAttempt;
        this.LastAttemptAtUtc = nowUtc;
        this.AttemptCount++;
        this.Version++;
        return Result.Success();
    }

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
        string normalizedDisposition = NormalizeCode(dispositionCode, OwnerCodeMaxLength);
        string normalizedReason = NormalizeCode(reasonCode, OwnerCodeMaxLength);
        string normalizedDigest = receiptSha256?.Trim().ToLowerInvariant() ?? string.Empty;

        if (this.State == DataRightsExecutionWorkItemState.OwnerProofRecorded)
        {
            return this.OwnerReceiptContractVersion == receiptContractVersion &&
                this.OwnerReceiptId == receiptId &&
                this.ResultingRecordVersion == resultingRecordVersion &&
                string.Equals(
                    this.OwnerDispositionCode,
                    normalizedDisposition,
                    StringComparison.Ordinal) &&
                string.Equals(this.OwnerReasonCode, normalizedReason, StringComparison.Ordinal) &&
                string.Equals(this.OwnerReceiptSha256, normalizedDigest, StringComparison.Ordinal) &&
                this.OwnerCompletedAtUtc == completedAtUtc
                    ? Result.Success()
                    : Result.Failure(DataRightsDomainErrors.ExecutionOwnerResultInvalid);
        }

        if (!this.CanRecordResult(expectedVersion, taskRunId, taskAttempt) ||
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
            return Result.Failure(DataRightsDomainErrors.ExecutionOwnerResultInvalid);
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

    public Result RecordBlocked(
        long expectedVersion,
        Guid taskRunId,
        int taskAttempt,
        string blockerCode,
        DateTimeOffset recordedAtUtc) =>
        this.RecordOwnerOutcome(
            expectedVersion,
            taskRunId,
            taskAttempt,
            blockerCode,
            DataRightsExecutionWorkItemState.Blocked,
            recordedAtUtc);

    public Result RecordFailed(
        long expectedVersion,
        Guid taskRunId,
        int taskAttempt,
        string failureCode,
        DateTimeOffset recordedAtUtc) =>
        this.RecordOwnerOutcome(
            expectedVersion,
            taskRunId,
            taskAttempt,
            failureCode,
            DataRightsExecutionWorkItemState.Failed,
            recordedAtUtc);

    private Result RecordOwnerOutcome(
        long expectedVersion,
        Guid taskRunId,
        int taskAttempt,
        string outcomeCode,
        DataRightsExecutionWorkItemState outcomeState,
        DateTimeOffset recordedAtUtc)
    {
        string normalizedCode = NormalizeCode(outcomeCode, OutcomeCodeMaxLength);
        if (this.State == outcomeState)
        {
            return string.Equals(this.OutcomeCode, normalizedCode, StringComparison.Ordinal)
                ? Result.Success()
                : Result.Failure(DataRightsDomainErrors.ExecutionOwnerResultInvalid);
        }

        if (outcomeState is not DataRightsExecutionWorkItemState.Blocked
                and not DataRightsExecutionWorkItemState.Failed ||
            !this.CanRecordResult(expectedVersion, taskRunId, taskAttempt) ||
            normalizedCode.Length == 0 ||
            recordedAtUtc == default ||
            recordedAtUtc < this.CreatedAtUtc)
        {
            return Result.Failure(DataRightsDomainErrors.ExecutionOwnerResultInvalid);
        }

        this.OutcomeCode = normalizedCode;
        this.OutcomeAtUtc = recordedAtUtc;
        this.State = outcomeState;
        this.Version++;
        return Result.Success();
    }

    private bool CanRecordResult(
        long expectedVersion,
        Guid taskRunId,
        int taskAttempt) =>
        this.State == DataRightsExecutionWorkItemState.Processing &&
        expectedVersion == this.Version &&
        taskRunId != Guid.Empty &&
        this.TaskRunId == taskRunId &&
        taskAttempt > 0 &&
        this.LastTaskAttempt == taskAttempt;

    private static string NormalizeCode(string? value, int maxLength)
    {
        string normalized = value?.Trim() ?? string.Empty;
        return normalized.Length <= maxLength ? normalized : string.Empty;
    }

    private static bool IsSha256(string value) =>
        value.Length == Sha256Length && value.All(Uri.IsHexDigit);
}

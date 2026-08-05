namespace BunkFy.Modules.DataRights.Domain.Aggregates;

using BunkFy.Modules.DataRights.Domain.Errors;
using BunkFy.Modules.DataRights.Domain.Models;
using Gma.Framework.Domain.Models;
using Gma.Framework.Naming;
using Gma.Framework.Results;

public sealed class TenantTerminationOwnerWorkItem : ScopedAggregateRoot<Guid>
{
    public const int OwnerKeyMaxLength = 100;
    public const int ResultCodeMaxLength = 200;
    public const int Sha256Length = 64;

    private TenantTerminationOwnerWorkItem() { }

    private TenantTerminationOwnerWorkItem(Guid id, string scopeId) : base(id, scopeId) { }

    public Guid ProcessId { get; private set; }
    public Guid CaseId { get; private set; }
    public long ApprovalRevision { get; private set; }
    public long OperationRevision { get; private set; }
    public Guid TerminationEpoch { get; private set; }
    public Guid IdempotencyKey { get; private set; }
    public TenantTerminationOwnerPhase Phase { get; private set; }
    public string OwnerKey { get; private set; } = string.Empty;
    public int OwnerContractVersion { get; private set; }
    public int CatalogVersion { get; private set; }
    public string CatalogSha256 { get; private set; } = string.Empty;
    public string PolicyEvidenceSha256 { get; private set; } = string.Empty;
    public TenantTerminationOwnerWorkState State { get; private set; }
    public int AttemptCount { get; private set; }
    public Guid? TaskRunId { get; private set; }
    public int LastTaskAttempt { get; private set; }
    public DateTimeOffset? LastAttemptAtUtc { get; private set; }
    public string? ResultCode { get; private set; }
    public long? AffectedCount { get; private set; }
    public long? RetainedMinimumCount { get; private set; }
    public long? RemainingActiveCount { get; private set; }
    public DateTimeOffset? HoldReviewAtUtc { get; private set; }
    public long? SelectedProofRevision { get; private set; }
    public long? ResultingProofRevision { get; private set; }
    public DateTimeOffset? ResultRecordedAtUtc { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset LastChangedAtUtc { get; private set; }
    public long Version { get; private set; } = 1;

    public static Result<TenantTerminationOwnerWorkItem> Prepare(
        Guid id,
        string tenantId,
        Guid processId,
        Guid caseId,
        long approvalRevision,
        long operationRevision,
        Guid terminationEpoch,
        Guid idempotencyKey,
        TenantTerminationOwnerPhase phase,
        string ownerKey,
        int ownerContractVersion,
        int catalogVersion,
        string catalogSha256,
        string policyEvidenceSha256,
        DateTimeOffset nowUtc)
    {
        if (id == Guid.Empty ||
            processId == Guid.Empty ||
            caseId == Guid.Empty ||
            approvalRevision <= 0 ||
            operationRevision <= 0 ||
            terminationEpoch == Guid.Empty ||
            idempotencyKey == Guid.Empty ||
            !Enum.IsDefined(phase) ||
            phase == TenantTerminationOwnerPhase.Unknown ||
            !IsStableKey(ownerKey, OwnerKeyMaxLength) ||
            ownerContractVersion <= 0 ||
            catalogVersion <= 0 ||
            !TenantTerminationProcess.IsSha256(catalogSha256) ||
            !TenantTerminationProcess.IsSha256(policyEvidenceSha256) ||
            nowUtc == default)
        {
            return Result.Failure<TenantTerminationOwnerWorkItem>(
                DataRightsDomainErrors.TenantTerminationOwnerWorkInvalid);
        }

        if (!TenantIds.TryNormalize(tenantId, out string? scopeId))
        {
            return Result.Failure<TenantTerminationOwnerWorkItem>(
                DataRightsDomainErrors.TenantInvalid);
        }

        return Result.Success(new TenantTerminationOwnerWorkItem(id, scopeId)
        {
            ProcessId = processId,
            CaseId = caseId,
            ApprovalRevision = approvalRevision,
            OperationRevision = operationRevision,
            TerminationEpoch = terminationEpoch,
            IdempotencyKey = idempotencyKey,
            Phase = phase,
            OwnerKey = ownerKey.Trim(),
            OwnerContractVersion = ownerContractVersion,
            CatalogVersion = catalogVersion,
            CatalogSha256 = catalogSha256,
            PolicyEvidenceSha256 = policyEvidenceSha256,
            State = TenantTerminationOwnerWorkState.Prepared,
            CreatedAtUtc = nowUtc,
            LastChangedAtUtc = nowUtc
        });
    }

    public Result BeginProcessing(
        Guid taskRunId,
        int taskAttempt,
        long expectedVersion,
        DateTimeOffset nowUtc)
    {
        if (this.State == TenantTerminationOwnerWorkState.Processing &&
            this.TaskRunId == taskRunId &&
            this.LastTaskAttempt == taskAttempt)
        {
            return Result.Success();
        }

        if (expectedVersion != this.Version)
        {
            return Result.Failure(DataRightsDomainErrors.VersionConflict);
        }

        if (this.State == TenantTerminationOwnerWorkState.Processing)
        {
            if (this.TaskRunId != taskRunId ||
                taskRunId == Guid.Empty ||
                taskAttempt <= this.LastTaskAttempt ||
                nowUtc == default ||
                nowUtc < this.LastChangedAtUtc)
            {
                return Result.Failure(
                    DataRightsDomainErrors.TenantTerminationOwnerWorkInvalid);
            }

            this.LastTaskAttempt = taskAttempt;
            this.LastAttemptAtUtc = nowUtc;
            this.AttemptCount++;
            this.LastChangedAtUtc = nowUtc;
            this.Version++;
            return Result.Success();
        }

        if (this.State is not TenantTerminationOwnerWorkState.Prepared and
                not TenantTerminationOwnerWorkState.RetryRequired ||
            taskRunId == Guid.Empty ||
            taskAttempt <= 0 ||
            (this.TaskRunId == taskRunId &&
                taskAttempt <= this.LastTaskAttempt) ||
            nowUtc == default ||
            nowUtc < this.LastChangedAtUtc)
        {
            return Result.Failure(
                DataRightsDomainErrors.TenantTerminationOwnerWorkInvalid);
        }

        this.State = TenantTerminationOwnerWorkState.Processing;
        this.TaskRunId = taskRunId;
        this.LastTaskAttempt = taskAttempt;
        this.LastAttemptAtUtc = nowUtc;
        this.AttemptCount++;
        this.ClearResult();
        this.LastChangedAtUtc = nowUtc;
        this.Version++;
        return Result.Success();
    }

    public Result RecordResult(
        TenantTerminationOwnerWorkState state,
        string resultCode,
        long affectedCount,
        long retainedMinimumCount,
        long remainingActiveCount,
        DateTimeOffset? holdReviewAtUtc,
        long? selectedProofRevision,
        long? resultingProofRevision,
        int catalogVersion,
        string catalogSha256,
        Guid taskRunId,
        int taskAttempt,
        long expectedVersion,
        DateTimeOffset recordedAtUtc)
    {
        if (this.IsExactResult(
                state,
                resultCode,
                affectedCount,
                retainedMinimumCount,
                remainingActiveCount,
                holdReviewAtUtc,
                selectedProofRevision,
                resultingProofRevision,
                catalogVersion,
                catalogSha256,
                taskRunId,
                taskAttempt,
                recordedAtUtc))
        {
            return Result.Success();
        }

        if (expectedVersion != this.Version)
        {
            return Result.Failure(DataRightsDomainErrors.VersionConflict);
        }

        if (this.State != TenantTerminationOwnerWorkState.Processing ||
            this.TaskRunId != taskRunId ||
            this.LastTaskAttempt != taskAttempt ||
            state is not (
                TenantTerminationOwnerWorkState.Completed or
                TenantTerminationOwnerWorkState.Blocked or
                TenantTerminationOwnerWorkState.RetryRequired or
                TenantTerminationOwnerWorkState.Failed) ||
            !TenantTerminationProcess.IsStableCode(
                resultCode,
                ResultCodeMaxLength) ||
            affectedCount < 0 ||
            retainedMinimumCount < 0 ||
            remainingActiveCount < 0 ||
            catalogVersion != this.CatalogVersion ||
            !string.Equals(
                catalogSha256,
                this.CatalogSha256,
                StringComparison.Ordinal) ||
            recordedAtUtc == default ||
            recordedAtUtc < this.LastChangedAtUtc ||
            !HasValidOutcomeShape(
                state,
                remainingActiveCount,
                holdReviewAtUtc,
                selectedProofRevision,
                resultingProofRevision,
                recordedAtUtc))
        {
            return Result.Failure(
                DataRightsDomainErrors.TenantTerminationOwnerWorkInvalid);
        }

        this.State = state;
        this.ResultCode = resultCode.Trim();
        this.AffectedCount = affectedCount;
        this.RetainedMinimumCount = retainedMinimumCount;
        this.RemainingActiveCount = remainingActiveCount;
        this.HoldReviewAtUtc = holdReviewAtUtc;
        this.SelectedProofRevision = selectedProofRevision;
        this.ResultingProofRevision = resultingProofRevision;
        this.ResultRecordedAtUtc = recordedAtUtc;
        this.LastChangedAtUtc = recordedAtUtc;
        this.Version++;
        return Result.Success();
    }

    public Result Requeue(long expectedVersion, DateTimeOffset nowUtc)
    {
        if (expectedVersion != this.Version)
        {
            return Result.Failure(DataRightsDomainErrors.VersionConflict);
        }

        if (this.State is not TenantTerminationOwnerWorkState.Blocked and
                not TenantTerminationOwnerWorkState.Failed ||
            nowUtc == default ||
            nowUtc < this.LastChangedAtUtc)
        {
            return Result.Failure(
                DataRightsDomainErrors.TenantTerminationOwnerWorkInvalid);
        }

        this.State = TenantTerminationOwnerWorkState.Prepared;
        this.TaskRunId = null;
        this.ClearResult();
        this.LastChangedAtUtc = nowUtc;
        this.Version++;
        return Result.Success();
    }

    public bool Matches(
        Guid processId,
        TenantTerminationOwnerPhase phase,
        string ownerKey,
        long operationRevision,
        Guid idempotencyKey) =>
        processId != Guid.Empty &&
        operationRevision > 0 &&
        idempotencyKey != Guid.Empty &&
        this.ProcessId == processId &&
        this.Phase == phase &&
        string.Equals(this.OwnerKey, ownerKey?.Trim(), StringComparison.Ordinal) &&
        this.OperationRevision == operationRevision &&
        this.IdempotencyKey == idempotencyKey;

    private bool IsExactResult(
        TenantTerminationOwnerWorkState state,
        string resultCode,
        long affectedCount,
        long retainedMinimumCount,
        long remainingActiveCount,
        DateTimeOffset? holdReviewAtUtc,
        long? selectedProofRevision,
        long? resultingProofRevision,
        int catalogVersion,
        string catalogSha256,
        Guid taskRunId,
        int taskAttempt,
        DateTimeOffset recordedAtUtc) =>
        this.State == state &&
        this.TaskRunId == taskRunId &&
        this.LastTaskAttempt == taskAttempt &&
        string.Equals(this.ResultCode, resultCode?.Trim(), StringComparison.Ordinal) &&
        this.AffectedCount == affectedCount &&
        this.RetainedMinimumCount == retainedMinimumCount &&
        this.RemainingActiveCount == remainingActiveCount &&
        this.HoldReviewAtUtc == holdReviewAtUtc &&
        this.SelectedProofRevision == selectedProofRevision &&
        this.ResultingProofRevision == resultingProofRevision &&
        this.CatalogVersion == catalogVersion &&
        string.Equals(this.CatalogSha256, catalogSha256, StringComparison.Ordinal) &&
        this.ResultRecordedAtUtc == recordedAtUtc;

    private static bool HasValidOutcomeShape(
        TenantTerminationOwnerWorkState state,
        long remainingActiveCount,
        DateTimeOffset? holdReviewAtUtc,
        long? selectedProofRevision,
        long? resultingProofRevision,
        DateTimeOffset recordedAtUtc) =>
        state switch
        {
            TenantTerminationOwnerWorkState.Completed =>
                remainingActiveCount == 0 &&
                !holdReviewAtUtc.HasValue &&
                selectedProofRevision is >= 0 &&
                resultingProofRevision >= selectedProofRevision,
            TenantTerminationOwnerWorkState.Blocked =>
                remainingActiveCount > 0 &&
                (!holdReviewAtUtc.HasValue ||
                 holdReviewAtUtc.Value >= recordedAtUtc) &&
                !selectedProofRevision.HasValue &&
                !resultingProofRevision.HasValue,
            TenantTerminationOwnerWorkState.RetryRequired or
                TenantTerminationOwnerWorkState.Failed =>
                !holdReviewAtUtc.HasValue &&
                !selectedProofRevision.HasValue &&
                !resultingProofRevision.HasValue,
            _ => false
        };

    private void ClearResult()
    {
        this.ResultCode = null;
        this.AffectedCount = null;
        this.RetainedMinimumCount = null;
        this.RemainingActiveCount = null;
        this.HoldReviewAtUtc = null;
        this.SelectedProofRevision = null;
        this.ResultingProofRevision = null;
        this.ResultRecordedAtUtc = null;
    }

    private static bool IsStableKey(string? value, int maxLength)
    {
        string normalized = value?.Trim() ?? string.Empty;
        return normalized.Length > 0 &&
            normalized.Length <= maxLength &&
            normalized[0] is >= 'a' and <= 'z' &&
            normalized.All(character =>
                character is (>= 'a' and <= 'z') or
                    (>= '0' and <= '9') or '.' or '-' or '_');
    }
}

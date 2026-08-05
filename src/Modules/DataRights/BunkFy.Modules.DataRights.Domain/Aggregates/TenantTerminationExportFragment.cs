namespace BunkFy.Modules.DataRights.Domain.Aggregates;

using BunkFy.Modules.DataRights.Domain.Errors;
using BunkFy.Modules.DataRights.Domain.Models;
using Gma.Framework.Domain.Models;
using Gma.Framework.Naming;
using Gma.Framework.Results;

public sealed partial class TenantTerminationExportFragment
    : ScopedAggregateRoot<Guid>
{
    public const int OwnerKeyMaxLength =
        TenantTerminationOwnerWorkItem.OwnerKeyMaxLength;
    public const int ResultCodeMaxLength =
        TenantTerminationOwnerWorkItem.ResultCodeMaxLength;
    public const int FailureCodeMaxLength = 100;
    public const int StorageKeyMaxLength = 500;
    public const int Sha256Length = TenantTerminationProcess.Sha256Length;
    public const int MaximumRecordCount = 1_000_000;

    private TenantTerminationExportFragment() { }

    private TenantTerminationExportFragment(Guid id, string scopeId)
        : base(id, scopeId) { }

    public Guid ProcessId { get; private set; }
    public Guid CaseId { get; private set; }
    public long ApprovalRevision { get; private set; }
    public long FreezeOperationRevision { get; private set; }
    public long ExportOperationRevision { get; private set; }
    public Guid TerminationEpoch { get; private set; }
    public Guid IdempotencyKey { get; private set; }
    public string OwnerKey { get; private set; } = string.Empty;
    public int OwnerContractVersion { get; private set; }
    public int CatalogVersion { get; private set; }
    public string CatalogSha256 { get; private set; } = string.Empty;
    public string FrozenRevisionSha256 { get; private set; } = string.Empty;
    public string PolicyEvidenceSha256 { get; private set; } = string.Empty;
    public TenantTerminationExportFragmentState State { get; private set; }
    public DateTimeOffset RequestedAtUtc { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public Guid? GenerationRunId { get; private set; }
    public int? GenerationAttempt { get; private set; }
    public DateTimeOffset? GenerationStartedAtUtc { get; private set; }
    public string? FailureCode { get; private set; }
    public long? RecordCount { get; private set; }
    public long? SelectedProofRevision { get; private set; }
    public long? ResultingProofRevision { get; private set; }
    public string? ResultCode { get; private set; }
    public string? StorageKey { get; private set; }
    public long? EncryptedByteLength { get; private set; }
    public string? PlaintextSha256 { get; private set; }
    public int? EncryptionKeyVersion { get; private set; }
    public int? FormatVersion { get; private set; }
    public DateTimeOffset? AvailableAtUtc { get; private set; }
    public Guid? DeletionRunId { get; private set; }
    public DateTimeOffset? DeletionStartedAtUtc { get; private set; }
    public DateTimeOffset? DeletedAtUtc { get; private set; }
    public long Version { get; private set; } = 1;

    public static Result<TenantTerminationExportFragment> Request(
        Guid workItemId,
        string tenantId,
        Guid processId,
        Guid caseId,
        long approvalRevision,
        long freezeOperationRevision,
        long exportOperationRevision,
        Guid terminationEpoch,
        Guid idempotencyKey,
        string ownerKey,
        int ownerContractVersion,
        int catalogVersion,
        string catalogSha256,
        string frozenRevisionSha256,
        string policyEvidenceSha256,
        DateTimeOffset nowUtc,
        DateTimeOffset expiresAtUtc)
    {
        if (workItemId == Guid.Empty ||
            processId == Guid.Empty ||
            caseId == Guid.Empty ||
            approvalRevision <= 0 ||
            freezeOperationRevision <= 0 ||
            exportOperationRevision <= freezeOperationRevision ||
            terminationEpoch == Guid.Empty ||
            idempotencyKey == Guid.Empty ||
            !IsStableKey(ownerKey) ||
            ownerContractVersion <= 0 ||
            catalogVersion <= 0 ||
            !IsSha256(catalogSha256) ||
            !IsSha256(frozenRevisionSha256) ||
            !IsSha256(policyEvidenceSha256) ||
            nowUtc == default ||
            expiresAtUtc <= nowUtc)
        {
            return Invalid<TenantTerminationExportFragment>();
        }

        if (!TenantIds.TryNormalize(tenantId, out string? scopeId))
        {
            return Result.Failure<TenantTerminationExportFragment>(
                DataRightsDomainErrors.TenantInvalid);
        }

        return Result.Success(new TenantTerminationExportFragment(
            workItemId,
            scopeId)
        {
            ProcessId = processId,
            CaseId = caseId,
            ApprovalRevision = approvalRevision,
            FreezeOperationRevision = freezeOperationRevision,
            ExportOperationRevision = exportOperationRevision,
            TerminationEpoch = terminationEpoch,
            IdempotencyKey = idempotencyKey,
            OwnerKey = ownerKey.Trim(),
            OwnerContractVersion = ownerContractVersion,
            CatalogVersion = catalogVersion,
            CatalogSha256 = catalogSha256,
            FrozenRevisionSha256 = frozenRevisionSha256,
            PolicyEvidenceSha256 = policyEvidenceSha256,
            State = TenantTerminationExportFragmentState.Requested,
            RequestedAtUtc = nowUtc,
            ExpiresAtUtc = expiresAtUtc
        });
    }

    public bool Matches(
        Guid processId,
        long exportOperationRevision,
        Guid idempotencyKey,
        string ownerKey,
        string frozenRevisionSha256) =>
        processId != Guid.Empty &&
        exportOperationRevision > 0 &&
        idempotencyKey != Guid.Empty &&
        this.ProcessId == processId &&
        this.ExportOperationRevision == exportOperationRevision &&
        this.IdempotencyKey == idempotencyKey &&
        string.Equals(
            this.OwnerKey,
            ownerKey?.Trim(),
            StringComparison.Ordinal) &&
        string.Equals(
            this.FrozenRevisionSha256,
            frozenRevisionSha256,
            StringComparison.Ordinal);

    public Result BeginGeneration(
        Guid runId,
        int attempt,
        DateTimeOffset nowUtc)
    {
        if (runId == Guid.Empty ||
            attempt <= 0 ||
            nowUtc == default ||
            nowUtc < this.RequestedAtUtc ||
            nowUtc >= this.ExpiresAtUtc)
        {
            return Result.Failure(
                DataRightsDomainErrors
                    .TenantTerminationExportFragmentGenerationInvalid);
        }

        if (this.State == TenantTerminationExportFragmentState.Available)
        {
            return this.GenerationRunId == runId &&
                this.GenerationAttempt == attempt
                ? Result.Success()
                : Result.Failure(
                    DataRightsDomainErrors
                        .TenantTerminationExportFragmentTransitionInvalid);
        }

        if (this.State == TenantTerminationExportFragmentState.Generating)
        {
            if (this.GenerationRunId != runId ||
                this.GenerationAttempt is not int currentAttempt ||
                attempt < currentAttempt)
            {
                return Result.Failure(
                    DataRightsDomainErrors
                        .TenantTerminationExportFragmentTransitionInvalid);
            }

            if (attempt == currentAttempt)
            {
                return Result.Success();
            }
        }

        bool retry = this.State == TenantTerminationExportFragmentState.Failed &&
            this.GenerationAttempt is int failedAttempt &&
            attempt > failedAttempt;
        bool takeover =
            this.State == TenantTerminationExportFragmentState.Generating;
        if (this.State != TenantTerminationExportFragmentState.Requested &&
            !retry &&
            !takeover)
        {
            return Result.Failure(
                DataRightsDomainErrors
                    .TenantTerminationExportFragmentTransitionInvalid);
        }

        this.State = TenantTerminationExportFragmentState.Generating;
        this.GenerationRunId = runId;
        this.GenerationAttempt = attempt;
        this.GenerationStartedAtUtc = nowUtc;
        this.FailureCode = null;
        this.ClearCompletion();
        this.Version++;
        return Result.Success();
    }

    public Result MarkFailed(
        Guid runId,
        int attempt,
        string failureCode,
        DateTimeOffset failedAtUtc)
    {
        string normalizedCode = failureCode?.Trim() ?? string.Empty;
        if (this.State == TenantTerminationExportFragmentState.Failed &&
            this.GenerationRunId == runId &&
            this.GenerationAttempt == attempt)
        {
            return string.Equals(
                this.FailureCode,
                normalizedCode,
                StringComparison.Ordinal)
                ? Result.Success()
                : Result.Failure(
                    DataRightsDomainErrors
                        .TenantTerminationExportFragmentFailureInvalid);
        }

        if (this.State != TenantTerminationExportFragmentState.Generating ||
            this.GenerationRunId != runId ||
            this.GenerationAttempt != attempt ||
            !IsStableCode(normalizedCode, FailureCodeMaxLength) ||
            this.GenerationStartedAtUtc is not DateTimeOffset startedAtUtc ||
            failedAtUtc < startedAtUtc)
        {
            return Result.Failure(
                DataRightsDomainErrors
                    .TenantTerminationExportFragmentFailureInvalid);
        }

        this.State = TenantTerminationExportFragmentState.Failed;
        this.FailureCode = normalizedCode;
        this.ClearCompletion();
        this.Version++;
        return Result.Success();
    }

    private static bool IsStableKey(string? value) =>
        IsStableCode(value, OwnerKeyMaxLength);

    private static bool IsStableCode(string? value, int maxLength)
    {
        string normalized = value?.Trim() ?? string.Empty;
        return normalized.Length > 0 &&
            normalized.Length <= maxLength &&
            normalized[0] is >= 'a' and <= 'z' &&
            normalized.All(character =>
                character is (>= 'a' and <= 'z') or
                    (>= '0' and <= '9') or '.' or '-' or '_');
    }

    private static bool IsSha256(string? value) =>
        value is { Length: Sha256Length } &&
        value.All(character =>
            character is (>= '0' and <= '9') or (>= 'a' and <= 'f'));

    private static Result<T> Invalid<T>() =>
        Result.Failure<T>(
            DataRightsDomainErrors.TenantTerminationExportFragmentInvalid);
}

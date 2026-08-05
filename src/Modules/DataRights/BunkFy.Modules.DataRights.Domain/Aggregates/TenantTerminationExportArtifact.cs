namespace BunkFy.Modules.DataRights.Domain.Aggregates;

using BunkFy.Modules.DataRights.Domain.Errors;
using BunkFy.Modules.DataRights.Domain.Models;
using Gma.Framework.Domain.Models;
using Gma.Framework.Naming;
using Gma.Framework.Results;

public sealed partial class TenantTerminationExportArtifact
    : ScopedAggregateRoot<Guid>
{
    public const int FailureCodeMaxLength = 100;
    public const int StorageKeyMaxLength = 500;
    public const int Sha256Length = TenantTerminationProcess.Sha256Length;
    public const int MaximumFragmentCount = 100;
    public const long MaximumRecordCount =
        MaximumFragmentCount *
        (long)TenantTerminationExportFragment.MaximumRecordCount;

    private TenantTerminationExportArtifact() { }

    private TenantTerminationExportArtifact(Guid id, string scopeId)
        : base(id, scopeId) { }

    public Guid ProcessId { get; private set; }
    public Guid CaseId { get; private set; }
    public long ApprovalRevision { get; private set; }
    public long FreezeOperationRevision { get; private set; }
    public long ExportOperationRevision { get; private set; }
    public Guid TerminationEpoch { get; private set; }
    public Guid IdempotencyKey { get; private set; }
    public string FrozenRevisionSha256 { get; private set; } = string.Empty;
    public string PolicyEvidenceSha256 { get; private set; } = string.Empty;
    public int ExpectedFragmentCount { get; private set; }
    public string FragmentSetSha256 { get; private set; } = string.Empty;
    public TenantTerminationExportArtifactState State { get; private set; }
    public DateTimeOffset RequestedAtUtc { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public Guid? GenerationRunId { get; private set; }
    public int? GenerationAttempt { get; private set; }
    public DateTimeOffset? GenerationStartedAtUtc { get; private set; }
    public string? FailureCode { get; private set; }
    public int? FragmentCount { get; private set; }
    public long? RecordCount { get; private set; }
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

    public static Result<TenantTerminationExportArtifact> Request(
        Guid artifactId,
        string tenantId,
        Guid processId,
        Guid caseId,
        long approvalRevision,
        long freezeOperationRevision,
        long exportOperationRevision,
        Guid terminationEpoch,
        Guid idempotencyKey,
        string frozenRevisionSha256,
        string policyEvidenceSha256,
        int expectedFragmentCount,
        string fragmentSetSha256,
        DateTimeOffset nowUtc,
        DateTimeOffset expiresAtUtc)
    {
        if (artifactId == Guid.Empty ||
            processId == Guid.Empty ||
            caseId == Guid.Empty ||
            approvalRevision <= 0 ||
            freezeOperationRevision <= 0 ||
            exportOperationRevision <= freezeOperationRevision ||
            terminationEpoch == Guid.Empty ||
            idempotencyKey == Guid.Empty ||
            !IsSha256(frozenRevisionSha256) ||
            !IsSha256(policyEvidenceSha256) ||
            expectedFragmentCount is <= 0 or > MaximumFragmentCount ||
            !IsSha256(fragmentSetSha256) ||
            nowUtc == default ||
            expiresAtUtc <= nowUtc)
        {
            return Invalid<TenantTerminationExportArtifact>();
        }

        if (!TenantIds.TryNormalize(tenantId, out string? scopeId))
        {
            return Result.Failure<TenantTerminationExportArtifact>(
                DataRightsDomainErrors.TenantInvalid);
        }

        return Result.Success(new TenantTerminationExportArtifact(
            artifactId,
            scopeId)
        {
            ProcessId = processId,
            CaseId = caseId,
            ApprovalRevision = approvalRevision,
            FreezeOperationRevision = freezeOperationRevision,
            ExportOperationRevision = exportOperationRevision,
            TerminationEpoch = terminationEpoch,
            IdempotencyKey = idempotencyKey,
            FrozenRevisionSha256 = frozenRevisionSha256,
            PolicyEvidenceSha256 = policyEvidenceSha256,
            ExpectedFragmentCount = expectedFragmentCount,
            FragmentSetSha256 = fragmentSetSha256,
            State = TenantTerminationExportArtifactState.Requested,
            RequestedAtUtc = nowUtc,
            ExpiresAtUtc = expiresAtUtc
        });
    }

    public bool Matches(
        Guid processId,
        long exportOperationRevision,
        Guid idempotencyKey,
        string frozenRevisionSha256,
        string fragmentSetSha256) =>
        processId != Guid.Empty &&
        exportOperationRevision > 0 &&
        idempotencyKey != Guid.Empty &&
        this.ProcessId == processId &&
        this.ExportOperationRevision == exportOperationRevision &&
        this.IdempotencyKey == idempotencyKey &&
        string.Equals(
            this.FrozenRevisionSha256,
            frozenRevisionSha256,
            StringComparison.Ordinal) &&
        string.Equals(
            this.FragmentSetSha256,
            fragmentSetSha256,
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
                    .TenantTerminationExportArtifactGenerationInvalid);
        }

        if (this.State == TenantTerminationExportArtifactState.Available)
        {
            return this.GenerationRunId == runId &&
                this.GenerationAttempt == attempt
                ? Result.Success()
                : Result.Failure(
                    DataRightsDomainErrors
                        .TenantTerminationExportArtifactTransitionInvalid);
        }

        if (this.State == TenantTerminationExportArtifactState.Generating)
        {
            if (this.GenerationRunId != runId ||
                this.GenerationAttempt is not int currentAttempt ||
                attempt < currentAttempt)
            {
                return Result.Failure(
                    DataRightsDomainErrors
                        .TenantTerminationExportArtifactTransitionInvalid);
            }

            if (attempt == currentAttempt)
            {
                return Result.Success();
            }
        }

        bool retry = this.State == TenantTerminationExportArtifactState.Failed &&
            this.GenerationAttempt is int failedAttempt &&
            attempt > failedAttempt;
        bool takeover =
            this.State == TenantTerminationExportArtifactState.Generating;
        if (this.State != TenantTerminationExportArtifactState.Requested &&
            !retry &&
            !takeover)
        {
            return Result.Failure(
                DataRightsDomainErrors
                    .TenantTerminationExportArtifactTransitionInvalid);
        }

        this.State = TenantTerminationExportArtifactState.Generating;
        this.GenerationRunId = runId;
        this.GenerationAttempt = attempt;
        this.GenerationStartedAtUtc = nowUtc;
        this.FailureCode = null;
        this.ClearCompletion();
        this.Version++;
        return Result.Success();
    }

    public Result MarkAvailable(
        Guid runId,
        int attempt,
        int fragmentCount,
        long recordCount,
        string fragmentSetSha256,
        string storageKey,
        long encryptedByteLength,
        string plaintextSha256,
        int encryptionKeyVersion,
        int formatVersion,
        DateTimeOffset availableAtUtc,
        DateTimeOffset expiresAtUtc)
    {
        string normalizedStorageKey = storageKey?.Trim() ?? string.Empty;
        if (this.State == TenantTerminationExportArtifactState.Available &&
            this.GenerationRunId == runId &&
            this.GenerationAttempt == attempt)
        {
            return this.IsExactCompletion(
                fragmentCount,
                recordCount,
                fragmentSetSha256,
                normalizedStorageKey,
                encryptedByteLength,
                plaintextSha256,
                encryptionKeyVersion,
                formatVersion,
                availableAtUtc,
                expiresAtUtc)
                ? Result.Success()
                : Result.Failure(
                    DataRightsDomainErrors
                        .TenantTerminationExportArtifactCompletionInvalid);
        }

        if (this.State != TenantTerminationExportArtifactState.Generating ||
            this.GenerationRunId != runId ||
            this.GenerationAttempt != attempt ||
            fragmentCount != this.ExpectedFragmentCount ||
            recordCount is < 0 or > MaximumRecordCount ||
            !string.Equals(
                fragmentSetSha256,
                this.FragmentSetSha256,
                StringComparison.Ordinal) ||
            normalizedStorageKey.Length is 0 or > StorageKeyMaxLength ||
            encryptedByteLength <= 0 ||
            !IsSha256(plaintextSha256) ||
            encryptionKeyVersion <= 0 ||
            formatVersion <= 0 ||
            this.GenerationStartedAtUtc is not DateTimeOffset startedAtUtc ||
            availableAtUtc < startedAtUtc ||
            expiresAtUtc != this.ExpiresAtUtc ||
            expiresAtUtc <= availableAtUtc)
        {
            return Result.Failure(
                DataRightsDomainErrors
                    .TenantTerminationExportArtifactCompletionInvalid);
        }

        this.State = TenantTerminationExportArtifactState.Available;
        this.FragmentCount = fragmentCount;
        this.RecordCount = recordCount;
        this.StorageKey = normalizedStorageKey;
        this.EncryptedByteLength = encryptedByteLength;
        this.PlaintextSha256 = plaintextSha256;
        this.EncryptionKeyVersion = encryptionKeyVersion;
        this.FormatVersion = formatVersion;
        this.AvailableAtUtc = availableAtUtc;
        this.FailureCode = null;
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
        if (this.State == TenantTerminationExportArtifactState.Failed &&
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
                        .TenantTerminationExportArtifactFailureInvalid);
        }

        if (this.State != TenantTerminationExportArtifactState.Generating ||
            this.GenerationRunId != runId ||
            this.GenerationAttempt != attempt ||
            !IsStableCode(normalizedCode, FailureCodeMaxLength) ||
            this.GenerationStartedAtUtc is not DateTimeOffset startedAtUtc ||
            failedAtUtc < startedAtUtc)
        {
            return Result.Failure(
                DataRightsDomainErrors
                    .TenantTerminationExportArtifactFailureInvalid);
        }

        this.State = TenantTerminationExportArtifactState.Failed;
        this.FailureCode = normalizedCode;
        this.ClearCompletion();
        this.Version++;
        return Result.Success();
    }

    private bool IsExactCompletion(
        int fragmentCount,
        long recordCount,
        string fragmentSetSha256,
        string storageKey,
        long encryptedByteLength,
        string plaintextSha256,
        int encryptionKeyVersion,
        int formatVersion,
        DateTimeOffset availableAtUtc,
        DateTimeOffset expiresAtUtc) =>
        this.FragmentCount == fragmentCount &&
        this.RecordCount == recordCount &&
        string.Equals(
            this.FragmentSetSha256,
            fragmentSetSha256,
            StringComparison.Ordinal) &&
        string.Equals(this.StorageKey, storageKey, StringComparison.Ordinal) &&
        this.EncryptedByteLength == encryptedByteLength &&
        string.Equals(
            this.PlaintextSha256,
            plaintextSha256,
            StringComparison.Ordinal) &&
        this.EncryptionKeyVersion == encryptionKeyVersion &&
        this.FormatVersion == formatVersion &&
        this.AvailableAtUtc == availableAtUtc &&
        this.ExpiresAtUtc == expiresAtUtc;

    private void ClearCompletion()
    {
        this.FragmentCount = null;
        this.RecordCount = null;
        this.StorageKey = null;
        this.EncryptedByteLength = null;
        this.PlaintextSha256 = null;
        this.EncryptionKeyVersion = null;
        this.FormatVersion = null;
        this.AvailableAtUtc = null;
    }

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
            DataRightsDomainErrors.TenantTerminationExportArtifactInvalid);
}

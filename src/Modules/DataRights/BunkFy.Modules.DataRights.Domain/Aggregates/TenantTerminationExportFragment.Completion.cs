namespace BunkFy.Modules.DataRights.Domain.Aggregates;

using BunkFy.Modules.DataRights.Domain.Errors;
using BunkFy.Modules.DataRights.Domain.Models;
using Gma.Framework.Results;

public sealed partial class TenantTerminationExportFragment
{
    public Result MarkAvailable(
        Guid runId,
        int attempt,
        long recordCount,
        long selectedProofRevision,
        long resultingProofRevision,
        string resultCode,
        int catalogVersion,
        string catalogSha256,
        string storageKey,
        long encryptedByteLength,
        string plaintextSha256,
        int encryptionKeyVersion,
        int formatVersion,
        DateTimeOffset availableAtUtc,
        DateTimeOffset expiresAtUtc)
    {
        string normalizedResultCode = resultCode?.Trim() ?? string.Empty;
        string normalizedStorageKey = storageKey?.Trim() ?? string.Empty;
        if (this.State == TenantTerminationExportFragmentState.Available &&
            this.GenerationRunId == runId &&
            this.GenerationAttempt == attempt)
        {
            return this.IsExactCompletion(
                recordCount,
                selectedProofRevision,
                resultingProofRevision,
                normalizedResultCode,
                catalogVersion,
                catalogSha256,
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
                        .TenantTerminationExportFragmentCompletionInvalid);
        }

        if (this.State != TenantTerminationExportFragmentState.Generating ||
            this.GenerationRunId != runId ||
            this.GenerationAttempt != attempt ||
            recordCount is < 0 or > MaximumRecordCount ||
            selectedProofRevision < 0 ||
            resultingProofRevision != selectedProofRevision ||
            !IsStableCode(normalizedResultCode, ResultCodeMaxLength) ||
            catalogVersion != this.CatalogVersion ||
            !string.Equals(
                catalogSha256,
                this.CatalogSha256,
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
                    .TenantTerminationExportFragmentCompletionInvalid);
        }

        this.State = TenantTerminationExportFragmentState.Available;
        this.RecordCount = recordCount;
        this.SelectedProofRevision = selectedProofRevision;
        this.ResultingProofRevision = resultingProofRevision;
        this.ResultCode = normalizedResultCode;
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

    private bool IsExactCompletion(
        long recordCount,
        long selectedProofRevision,
        long resultingProofRevision,
        string resultCode,
        int catalogVersion,
        string catalogSha256,
        string storageKey,
        long encryptedByteLength,
        string plaintextSha256,
        int encryptionKeyVersion,
        int formatVersion,
        DateTimeOffset availableAtUtc,
        DateTimeOffset expiresAtUtc) =>
        this.RecordCount == recordCount &&
        this.SelectedProofRevision == selectedProofRevision &&
        this.ResultingProofRevision == resultingProofRevision &&
        string.Equals(this.ResultCode, resultCode, StringComparison.Ordinal) &&
        this.CatalogVersion == catalogVersion &&
        string.Equals(
            this.CatalogSha256,
            catalogSha256,
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
        this.RecordCount = null;
        this.SelectedProofRevision = null;
        this.ResultingProofRevision = null;
        this.ResultCode = null;
        this.StorageKey = null;
        this.EncryptedByteLength = null;
        this.PlaintextSha256 = null;
        this.EncryptionKeyVersion = null;
        this.FormatVersion = null;
        this.AvailableAtUtc = null;
    }
}

namespace BunkFy.Modules.DataRights.Persistence;

using BunkFy.Modules.DataRights.Contracts;

internal abstract record DataRightsExportProtectionContext(
    Guid ObjectId,
    string TenantId,
    DateTimeOffset ExpiresAtUtc);

internal sealed record DataRightsSubjectExportProtectionContext(
    Guid ArtifactId,
    string TenantId,
    Guid CaseId,
    DataRightsCaseType CaseType,
    Guid? PropertyId,
    long DecisionRevision,
    DateTimeOffset ExpiresAtUtc)
    : DataRightsExportProtectionContext(
        ArtifactId,
        TenantId,
        ExpiresAtUtc);

internal sealed record TenantTerminationExportFragmentProtectionContext(
    Guid FragmentId,
    string TenantId,
    Guid ProcessId,
    Guid CaseId,
    long ApprovalRevision,
    long FreezeOperationRevision,
    long ExportOperationRevision,
    Guid TerminationEpoch,
    string OwnerKey,
    int OwnerContractVersion,
    int CatalogVersion,
    string CatalogSha256,
    string FrozenRevisionSha256,
    string PolicyEvidenceSha256,
    Guid GenerationRunId,
    int GenerationAttempt,
    DateTimeOffset ExpiresAtUtc)
    : DataRightsExportProtectionContext(
        FragmentId,
        TenantId,
        ExpiresAtUtc);

internal sealed record TenantTerminationExportArtifactProtectionContext(
    Guid ArtifactId,
    string TenantId,
    Guid ProcessId,
    Guid CaseId,
    long ApprovalRevision,
    long FreezeOperationRevision,
    long ExportOperationRevision,
    Guid TerminationEpoch,
    string FrozenRevisionSha256,
    string PolicyEvidenceSha256,
    int ExpectedFragmentCount,
    string FragmentSetSha256,
    Guid GenerationRunId,
    int GenerationAttempt,
    DateTimeOffset ExpiresAtUtc)
    : DataRightsExportProtectionContext(
        ArtifactId,
        TenantId,
        ExpiresAtUtc);

internal sealed record DataRightsExportProtectionResult(
    int FormatVersion,
    int KeyVersion,
    long PlaintextLength,
    long EncryptedLength);

internal sealed record DataRightsProtectedExportObject(
    string StorageKey,
    long EncryptedByteLength,
    string PlaintextSha256,
    int EncryptionKeyVersion,
    int FormatVersion,
    DateTimeOffset AvailableAtUtc,
    DateTimeOffset ExpiresAtUtc);

internal sealed record DataRightsProtectedExportReadRequest(
    string StorageKey,
    long EncryptedByteLength,
    string PlaintextSha256,
    int EncryptionKeyVersion,
    int FormatVersion,
    DataRightsExportProtectionContext ProtectionContext);

internal interface IDataRightsExportEnvelopeProtector
{
    Task<DataRightsExportProtectionResult> ProtectAsync(
        Stream plaintext,
        long plaintextLength,
        Stream encrypted,
        DataRightsExportProtectionContext context,
        CancellationToken cancellationToken);

    Task<DataRightsExportProtectionResult> UnprotectAsync(
        Stream encrypted,
        Stream plaintext,
        DataRightsExportProtectionContext context,
        CancellationToken cancellationToken);
}

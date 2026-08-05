namespace BunkFy.Modules.DataRights.Application.Models;

public sealed record TenantTerminationExportFragmentAssemblyRequest(
    string TenantId,
    Guid ProcessId,
    Guid CaseId,
    long ApprovalRevision,
    long FreezeOperationRevision,
    long ExportOperationRevision,
    Guid TerminationEpoch,
    long WorkspaceFenceRevision,
    string FrozenRevisionSha256,
    string PolicyEvidenceSha256,
    string ExecutingActorId,
    DateTimeOffset FrozenAtUtc,
    DateTimeOffset GeneratedAtUtc,
    DateTimeOffset DeadlineUtc,
    TenantTerminationExportOwnerWork OwnerWork,
    IReadOnlyCollection<TenantTerminationExportOwnerCatalogEntry> FrozenOwners);

public sealed record TenantTerminationExportOwnerWork(
    string OwnerKey,
    Guid WorkItemId,
    Guid IdempotencyKey,
    int ContractVersion,
    int CatalogVersion,
    string CatalogSha256);

public sealed record TenantTerminationExportOwnerCatalogEntry(
    string OwnerKey,
    int ContractVersion,
    int CatalogVersion,
    string CatalogSha256);

public sealed record TenantTerminationFrozenRevision(
    string TenantId,
    Guid ProcessId,
    Guid CaseId,
    long ApprovalRevision,
    long FreezeOperationRevision,
    Guid TerminationEpoch,
    long WorkspaceFenceRevision,
    string PolicyEvidenceSha256,
    DateTimeOffset FrozenAtUtc,
    IReadOnlyCollection<TenantTerminationExportOwnerCatalogEntry> FrozenOwners);

public sealed record TenantTerminationExportFragmentAssemblyResult(
    string FrozenRevisionSha256,
    TenantTerminationExportOwnerResult Owner);

public sealed record TenantTerminationExportOwnerResult(
    string OwnerKey,
    long RecordCount,
    long SelectedProofRevision,
    long ResultingProofRevision,
    string ResultCode,
    DateTimeOffset RecordedAtUtc);

public sealed record TenantTerminationExportFragmentGenerationRequest(
    TenantTerminationExportFragmentAssemblyRequest AssemblyRequest,
    Guid GenerationRunId,
    int GenerationAttempt,
    DateTimeOffset ExpiresAtUtc);

public sealed record TenantTerminationProtectedExportFragment(
    TenantTerminationExportFragmentAssemblyResult AssemblyResult,
    string StorageKey,
    long EncryptedByteLength,
    string PlaintextSha256,
    int EncryptionKeyVersion,
    int FormatVersion,
    DateTimeOffset AvailableAtUtc,
    DateTimeOffset ExpiresAtUtc);

public sealed record TenantTerminationProtectedExportArtifact(
    int FragmentCount,
    long RecordCount,
    string FragmentSetSha256,
    string StorageKey,
    long EncryptedByteLength,
    string PlaintextSha256,
    int EncryptionKeyVersion,
    int FormatVersion,
    DateTimeOffset AvailableAtUtc,
    DateTimeOffset ExpiresAtUtc);

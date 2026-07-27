namespace BunkFy.Modules.DataRights.Persistence;

using BunkFy.Modules.DataRights.Contracts;

internal sealed record DataRightsExportProtectionContext(
    Guid ArtifactId,
    string TenantId,
    Guid CaseId,
    DataRightsCaseType CaseType,
    Guid? PropertyId,
    long DecisionRevision,
    DateTimeOffset ExpiresAtUtc);

internal sealed record DataRightsExportProtectionResult(
    int FormatVersion,
    int KeyVersion,
    long PlaintextLength,
    long EncryptedLength);

internal interface IDataRightsExportArtifactProtector
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

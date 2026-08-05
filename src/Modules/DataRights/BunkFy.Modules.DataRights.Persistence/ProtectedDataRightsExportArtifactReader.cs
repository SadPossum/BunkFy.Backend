namespace BunkFy.Modules.DataRights.Persistence;

using BunkFy.Modules.DataRights.Application.Models;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Domain.Aggregates;

internal sealed class ProtectedDataRightsExportArtifactReader(
    ProtectedDataRightsExportObjectReader reader)
    : IDataRightsExportArtifactReader
{
    public async Task<DataRightsExportDownload> OpenVerifiedAsync(
        DataRightsExportArtifact artifact,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        FileStream plaintext = await reader.OpenVerifiedAsync(
            new DataRightsProtectedExportReadRequest(
                artifact.StorageKey ?? string.Empty,
                artifact.EncryptedByteLength ?? 0,
                artifact.PlaintextSha256 ?? string.Empty,
                artifact.EncryptionKeyVersion ?? 0,
                artifact.FormatVersion ?? 0,
                Context(artifact)),
            cancellationToken).ConfigureAwait(false);
        return new DataRightsExportDownload(
            plaintext,
            plaintext.Length,
            $"bunkfy-data-export-{artifact.Id:N}.json");
    }

    private static DataRightsSubjectExportProtectionContext Context(
        DataRightsExportArtifact artifact) => new(
        artifact.Id,
        artifact.ScopeId,
        artifact.CaseId,
        (DataRightsCaseType)artifact.CaseKind,
        artifact.PropertyId,
        artifact.DecisionRevision,
        artifact.ExpiresAtUtc);
}

namespace BunkFy.Modules.DataRights.Persistence;

using System.Security.Cryptography;
using BunkFy.Modules.DataRights.Application.Models;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using Gma.Framework.FileManagement;
using Microsoft.Extensions.Options;

internal sealed class ProtectedDataRightsExportArtifactReader(
    IDataRightsExportArtifactProtector protector,
    IFileStorage storage,
    IOptions<DataRightsExportArtifactOptions> options)
    : IDataRightsExportArtifactReader
{
    private const int MaximumEncryptionOverheadBytes = 512 * 1024;
    private readonly DataRightsExportArtifactOptions options = options.Value;

    public async Task<DataRightsExportDownload> OpenVerifiedAsync(
        DataRightsExportArtifact artifact,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        if (string.IsNullOrWhiteSpace(artifact.StorageKey) ||
            artifact.EncryptedByteLength is not > 0 ||
            string.IsNullOrWhiteSpace(artifact.PlaintextSha256) ||
            artifact.EncryptionKeyVersion is not > 0 ||
            artifact.FormatVersion is not > 0)
        {
            throw new DataRightsExportGenerationException(
                "artifact-metadata-invalid");
        }

        FileStorageObjectKey key = new(artifact.StorageKey);
        FileStorageReadResult? stored = await storage.OpenReadAsync(
            key,
            cancellationToken).ConfigureAwait(false);
        if (stored is null ||
            stored.Properties.ContentLength != artifact.EncryptedByteLength)
        {
            throw new DataRightsExportGenerationException(
                "stored-artifact-unavailable");
        }

        long maximumEncryptedBytes = checked(
            this.options.MaximumPlaintextBytes +
            MaximumEncryptionOverheadBytes);
        await using FileStream encrypted = DataRightsExportTemporaryFile.Create();
        BoundedWriteStream boundedEncrypted = new(
            encrypted,
            maximumEncryptedBytes);
        await stored.CopyToAsync(
            boundedEncrypted,
            cancellationToken).ConfigureAwait(false);
        await boundedEncrypted.FlushAsync(cancellationToken)
            .ConfigureAwait(false);
        if (encrypted.Length != artifact.EncryptedByteLength)
        {
            throw new DataRightsExportGenerationException(
                "stored-length-mismatch");
        }

        FileStream plaintext = DataRightsExportTemporaryFile.Create();
        try
        {
            BoundedWriteStream boundedPlaintext = new(
                plaintext,
                this.options.MaximumPlaintextBytes);
            encrypted.Position = 0;
            DataRightsExportProtectionResult result =
                await protector.UnprotectAsync(
                    encrypted,
                    boundedPlaintext,
                    Context(artifact),
                    cancellationToken).ConfigureAwait(false);
            await boundedPlaintext.FlushAsync(cancellationToken)
                .ConfigureAwait(false);
            if (result.EncryptedLength != artifact.EncryptedByteLength ||
                result.PlaintextLength != plaintext.Length ||
                result.KeyVersion != artifact.EncryptionKeyVersion ||
                result.FormatVersion != artifact.FormatVersion)
            {
                throw new DataRightsExportGenerationException(
                    "artifact-verification-failed");
            }

            plaintext.Position = 0;
            byte[] actualHash = await SHA256.HashDataAsync(
                plaintext,
                cancellationToken).ConfigureAwait(false);
            byte[] expectedHash = Convert.FromHexString(
                artifact.PlaintextSha256);
            try
            {
                if (!CryptographicOperations.FixedTimeEquals(
                        actualHash,
                        expectedHash))
                {
                    throw new DataRightsExportGenerationException(
                        "artifact-verification-failed");
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(actualHash);
                CryptographicOperations.ZeroMemory(expectedHash);
            }

            plaintext.Position = 0;
            return new DataRightsExportDownload(
                plaintext,
                plaintext.Length,
                $"bunkfy-data-export-{artifact.Id:N}.json");
        }
        catch
        {
            await plaintext.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private static DataRightsExportProtectionContext Context(
        DataRightsExportArtifact artifact) => new(
        artifact.Id,
        artifact.ScopeId,
        artifact.CaseId,
        (DataRightsCaseType)artifact.CaseKind,
        artifact.PropertyId,
        artifact.DecisionRevision,
        artifact.ExpiresAtUtc);

}

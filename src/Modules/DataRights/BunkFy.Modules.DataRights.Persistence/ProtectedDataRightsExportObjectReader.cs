namespace BunkFy.Modules.DataRights.Persistence;

using System.Security.Cryptography;
using BunkFy.Modules.DataRights.Application.Models;
using Gma.Framework.FileManagement;
using Microsoft.Extensions.Options;

internal sealed class ProtectedDataRightsExportObjectReader(
    IDataRightsExportEnvelopeProtector protector,
    IFileStorage storage,
    IOptions<DataRightsExportArtifactOptions> options)
{
    private readonly DataRightsExportArtifactOptions options = options.Value;

    public async Task<FileStream> OpenVerifiedAsync(
        DataRightsProtectedExportReadRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.StorageKey) ||
            request.EncryptedByteLength <= 0 ||
            !IsSha256(request.PlaintextSha256) ||
            request.EncryptionKeyVersion <= 0 ||
            request.FormatVersion <= 0)
        {
            throw new DataRightsExportGenerationException(
                "artifact-metadata-invalid");
        }

        FileStorageObjectKey key = new(request.StorageKey);
        FileStorageReadResult? stored = await storage.OpenReadAsync(
            key,
            cancellationToken).ConfigureAwait(false);
        if (stored is null ||
            stored.Properties.ContentLength != request.EncryptedByteLength)
        {
            throw new DataRightsExportGenerationException(
                "stored-artifact-unavailable");
        }

        long maximumEncryptedBytes = checked(
            this.options.MaximumPlaintextBytes +
            DataRightsExportArtifactOptions.MaximumEncryptionOverheadBytes);
        await using FileStream encrypted = DataRightsExportTemporaryFile.Create();
        BoundedWriteStream boundedEncrypted = new(
            encrypted,
            maximumEncryptedBytes);
        try
        {
            await stored.CopyToAsync(
                boundedEncrypted,
                cancellationToken).ConfigureAwait(false);
            await boundedEncrypted.FlushAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (DataRightsExportBufferLimitException exception)
        {
            throw new DataRightsExportGenerationException(
                "stored-artifact-limit-exceeded",
                exception);
        }

        if (encrypted.Length != request.EncryptedByteLength)
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
                    request.ProtectionContext,
                    cancellationToken).ConfigureAwait(false);
            await boundedPlaintext.FlushAsync(cancellationToken)
                .ConfigureAwait(false);
            if (result.EncryptedLength != request.EncryptedByteLength ||
                result.PlaintextLength != plaintext.Length ||
                result.KeyVersion != request.EncryptionKeyVersion ||
                result.FormatVersion != request.FormatVersion)
            {
                throw new DataRightsExportGenerationException(
                    "artifact-verification-failed");
            }

            plaintext.Position = 0;
            byte[] actualHash = await SHA256.HashDataAsync(
                plaintext,
                cancellationToken).ConfigureAwait(false);
            byte[] expectedHash = Convert.FromHexString(
                request.PlaintextSha256);
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
            return plaintext;
        }
        catch
        {
            await plaintext.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private static bool IsSha256(string value) =>
        value.Length == 64 &&
        value.All(character =>
            character is (>= '0' and <= '9') or (>= 'a' and <= 'f'));
}

namespace BunkFy.Modules.DataRights.Persistence;

using System.Security.Cryptography;
using BunkFy.Modules.DataRights.Application.Models;
using BunkFy.Modules.DataRights.Application.Ports;
using Gma.Framework.FileManagement;
using Gma.Framework.Runtime.Time;
using Microsoft.Extensions.Options;

internal sealed class ProtectedDataRightsExportArtifactGenerator(
    IDataRightsExportAssembler assembler,
    IDataRightsExportArtifactProtector protector,
    IFileStorage storage,
    IOptions<DataRightsExportArtifactOptions> options,
    ISystemClock clock)
    : IDataRightsExportArtifactGenerator
{
    private readonly DataRightsExportArtifactOptions options = options.Value;

    public async Task<DataRightsProtectedExportArtifact> GenerateAsync(
        DataRightsExportGenerationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        await using FileStream plaintext = DataRightsExportTemporaryFile.Create();
        BoundedWriteStream boundedPlaintext = new(
            plaintext,
            this.options.MaximumPlaintextBytes);
        try
        {
            _ = await assembler.AssembleAsync(
                request,
                boundedPlaintext,
                cancellationToken).ConfigureAwait(false);
            await boundedPlaintext.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DataRightsExportBufferLimitException exception)
        {
            throw new DataRightsExportGenerationException(
                "plaintext-limit-exceeded",
                exception);
        }

        long plaintextLength = plaintext.Length;
        if (plaintextLength is <= 0 ||
            plaintextLength > this.options.MaximumPlaintextBytes)
        {
            throw new DataRightsExportGenerationException(
                "plaintext-limit-exceeded");
        }

        plaintext.Position = 0;
        byte[] plaintextHash = await SHA256.HashDataAsync(
            plaintext,
            cancellationToken).ConfigureAwait(false);
        string plaintextSha256 = Convert.ToHexStringLower(plaintextHash);
        DateTimeOffset availableAtUtc = clock.UtcNow;
        DateTimeOffset expiresAtUtc = request.ExpiresAtUtc;
        if (expiresAtUtc <= availableAtUtc)
        {
            throw new DataRightsExportGenerationException(
                "artifact-expired");
        }
        DataRightsExportProtectionContext protectionContext = new(
            request.ArtifactId,
            request.TenantId,
            request.CaseId,
            request.CaseType,
            request.PropertyId,
            request.DecisionRevision,
            expiresAtUtc);

        try
        {
            plaintext.Position = 0;
            await using FileStream encrypted =
                DataRightsExportTemporaryFile.Create();
            DataRightsExportProtectionResult protectedResult =
                await protector.ProtectAsync(
                    plaintext,
                    plaintextLength,
                    encrypted,
                    protectionContext,
                    cancellationToken).ConfigureAwait(false);
            await encrypted.FlushAsync(cancellationToken).ConfigureAwait(false);
            if (encrypted.Length != protectedResult.EncryptedLength)
            {
                throw new DataRightsExportGenerationException(
                    "encrypted-length-mismatch");
            }

            FileStorageObjectKey storageKey = DataRightsExportStorageKey.Create(
                request.TenantId,
                request.ArtifactId);
            encrypted.Position = 0;
            FileStorageObjectProperties stored = await storage.PutAsync(
                new FileStorageWriteRequest(
                    storageKey,
                    encrypted,
                    encrypted.Length,
                    "application/octet-stream",
                    fileName: null,
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["module"] = DataRights.Contracts.DataRightsModuleMetadata.Name,
                        ["format"] = "bfdrx-1",
                        ["key-version"] =
                            protectedResult.KeyVersion.ToString(
                                System.Globalization.CultureInfo.InvariantCulture)
                    }),
                cancellationToken).ConfigureAwait(false);
            if (stored.ContentLength != protectedResult.EncryptedLength)
            {
                throw new DataRightsExportGenerationException(
                    "stored-length-mismatch");
            }

            await this.VerifyStoredAsync(
                storageKey,
                protectionContext,
                protectedResult,
                plaintextHash,
                cancellationToken).ConfigureAwait(false);
            return new DataRightsProtectedExportArtifact(
                storageKey.Value,
                protectedResult.EncryptedLength,
                plaintextSha256,
                protectedResult.KeyVersion,
                protectedResult.FormatVersion,
                availableAtUtc,
                expiresAtUtc);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintextHash);
        }
    }

    private async Task VerifyStoredAsync(
        FileStorageObjectKey storageKey,
        DataRightsExportProtectionContext protectionContext,
        DataRightsExportProtectionResult expected,
        byte[] expectedPlaintextHash,
        CancellationToken cancellationToken)
    {
        FileStorageReadResult? stored = await storage.OpenReadAsync(
            storageKey,
            cancellationToken).ConfigureAwait(false);
        if (stored is null ||
            stored.Properties.ContentLength != expected.EncryptedLength)
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

        if (encrypted.Length != expected.EncryptedLength)
        {
            throw new DataRightsExportGenerationException(
                "stored-length-mismatch");
        }

        encrypted.Position = 0;
        using HashingNullWriteStream verifiedPlaintext = new();
        DataRightsExportProtectionResult verified =
            await protector.UnprotectAsync(
                encrypted,
                verifiedPlaintext,
                protectionContext,
                cancellationToken).ConfigureAwait(false);
        byte[] actualHash = verifiedPlaintext.CompleteHash();
        try
        {
            if (verified.FormatVersion != expected.FormatVersion ||
                verified.KeyVersion != expected.KeyVersion ||
                verified.PlaintextLength != expected.PlaintextLength ||
                verified.EncryptedLength != expected.EncryptedLength ||
                verifiedPlaintext.Length != expected.PlaintextLength ||
                !CryptographicOperations.FixedTimeEquals(
                    actualHash,
                    expectedPlaintextHash))
            {
                throw new DataRightsExportGenerationException(
                    "stored-artifact-verification-failed");
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(actualHash);
        }
    }

}
